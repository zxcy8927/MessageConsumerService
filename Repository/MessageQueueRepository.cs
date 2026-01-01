using MessageConsumerService.Domain;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
namespace MessageConsumerService.Repository
{
    public class MessageQueueRepository
    {
        private readonly string _connStr;

        public MessageQueueRepository(IConfiguration config)
        {
            _connStr = config.GetConnectionString("DefaultConnection");
        }

        /// <summary>
        /// 取得待處理訊息數量
        /// </summary>
        public async Task<int> GetPendingCountAsync()
        {
            using var conn = new SqlConnection(_connStr);
            return await conn.QueryFirstOrDefaultAsync<int>(@"
                SELECT COUNT(*) 
                FROM MESSAGE_QUEUE 
                WHERE STATUS = 'PENDING'
                AND (LOCKED_UNTIL IS NULL OR LOCKED_UNTIL < GETDATE())
             ");
        }
        /// <summary>
        /// 取得一筆待處理訊息
        /// </summary>
        /// <param name="nodeName">哪個節點在搶訊息</param>
        /// <returns>成功搶到訊息回傳物件，沒搶到回傳 null</returns>
        public async Task<MessageEntity?> ClaimMessageAsync(string nodeName)
        {
            using var conn = new SqlConnection(_connStr);
            //設定鎖定期限 30 秒 (LOCKED_UNTIL),避免短時間被其他節點重複搶並回傳更新後的整筆訊息
            var sql = @"
                WITH CTE AS (
                    SELECT TOP 1 *
                    FROM MESSAGE_QUEUE WITH (UPDLOCK, READPAST)
                    WHERE STATUS = 'PENDING'
                      AND (LOCKED_UNTIL IS NULL OR LOCKED_UNTIL < GETDATE())
                    ORDER BY CREATED_AT
                )
                UPDATE CTE
                SET
                    STATUS = 'PROCESSING',
                    LOCKED_BY = @NodeName,
                    LOCKED_UNTIL = DATEADD(SECOND, 30, GETDATE())
                OUTPUT inserted.*;
             ";

            return await conn.QueryFirstOrDefaultAsync<MessageEntity>(
                sql, new { NodeName = nodeName });
        }

        /// <summary>
        /// 標記完成
        /// </summary>
        /// <param name="messageId"></param>
        /// <returns></returns>
        public async Task MarkDoneAsync(Guid messageId)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.ExecuteAsync(
                "UPDATE MESSAGE_QUEUE SET STATUS = 'DONE' WHERE MESSAGE_ID = @id",
                new { id = messageId });
        }
        /// <summary>
        /// 處理訊息失敗
        /// </summary>
        /// <param name="msg">要處理的訊息物件</param>
        /// <param name="reason">失敗原因</param>
        /// <returns></returns>
        public async Task ProcessFailedAsync(MessageEntity msg, string reason)
        {
            using var conn = new SqlConnection(_connStr);
            await conn.OpenAsync();

            using var tx = conn.BeginTransaction();

            try
            {
                //判斷重試次數
                if (msg.RETRY_COUNT >= 3)
                {
                    //進DLQ
                    await conn.ExecuteAsync(@"
                    INSERT INTO DEAD_LETTER_QUEUE
                    (MESSAGE_ID, MESSAGE_TYPE, PAYLOAD, FAILED_REASON)
                    VALUES (@Id, @Type, @Payload, @Reason)",
                        new
                        {
                            Id = msg.MESSAGE_ID,
                            Type = msg.MESSAGE_TYPE,
                            Payload = msg.PAYLOAD,
                            Reason = reason
                        }, tx);
                    //從主queue刪除以免阻塞
                    await conn.ExecuteAsync(
                        "DELETE FROM MESSAGE_QUEUE WHERE MESSAGE_ID = @Id",
                        new { Id = msg.MESSAGE_ID }, tx);
                }
                else
                {
                    //計算等待秒數，使用使數退避，避免短時間造成系統壓力
                    var delaySeconds = Math.Min((int)Math.Pow(2, msg.RETRY_COUNT), 60);

                    await conn.ExecuteAsync(@"
                    UPDATE MESSAGE_QUEUE
                    SET
                        STATUS = 'PENDING',
                        RETRY_COUNT = RETRY_COUNT + 1,
                        LOCKED_UNTIL = DATEADD(SECOND, @Delay, GETDATE())
                    WHERE MESSAGE_ID = @Id",
                    new { Id = msg.MESSAGE_ID, Delay = delaySeconds }, tx);
                }

                await conn.ExecuteAsync(@"
                INSERT INTO MESSAGE_LOG (MESSAGE_ID, STATUS, NODE_NAME)
                VALUES (@Id, 'FAILED', @Node)",
                new { Id = msg.MESSAGE_ID, Node = Environment.MachineName }, tx);

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }
}
