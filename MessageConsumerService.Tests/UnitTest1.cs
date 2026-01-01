using Dapper;
using MessageConsumerService.Repository;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Data.SqlClient;
using Xunit;
using Xunit.Abstractions;

namespace MessageConsumerService.Tests;

public class UnitTest1
{
    private readonly string _connStr;
    private readonly IConfiguration _config;
    private readonly ITestOutputHelper _output;

    public UnitTest1(ITestOutputHelper output)
    {
        _config = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        _connStr = _config.GetConnectionString("DefaultConnection");
        _output = output;
    }
    /// <summary>
    /// 準備測試資料，狀態為pendding
    /// </summary>
    /// <returns></returns>
    private async Task SeedMessagesAsync()
    {
        using var conn = new SqlConnection(_connStr);
        await conn.ExecuteAsync("DELETE FROM MESSAGE_QUEUE");
        await conn.ExecuteAsync(@"
            INSERT INTO MESSAGE_QUEUE
            (MESSAGE_ID, MESSAGE_TYPE, PAYLOAD, STATUS, RETRY_COUNT, CREATED_AT)
            VALUES
            (NEWID(), 'T1', '{}', 'PENDING', 0, GETDATE()),
            (NEWID(), 'T2', '{}', 'PENDING', 0, GETDATE()),
            (NEWID(), 'T3', '{}', 'PENDING', 0, GETDATE())
        ");
    }
    /// <summary>
    /// 驗證多個節點同時搶訊息時，不會有重複領取的情況
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task ClaimMessage_Should_Not_Be_Duplicated()
    {
        // Arrange
        await SeedMessagesAsync();
        var repo = new MessageQueueRepository(_config);
        //因使用多執行續
        var results = new ConcurrentBag<Guid?>();

        // 模擬10個節點同時搶
        var tasks = Enumerable.Range(0, 10)
            .Select(i => Task.Run(async () =>
            {
                var msg = await repo.ClaimMessageAsync($"NODE-{i}");
                //加入搶到的訊息ID
                results.Add(msg?.MESSAGE_ID);
            }));

        await Task.WhenAll(tasks);

        // Assert
        var claimed = results.Where(x => x.HasValue).ToList();

        // 應該正好搶到 3 筆 (因為只有 3 筆資料)
        Assert.Equal(3, claimed.Count);

        // 不會有重複 MESSAGE_ID
        Assert.Equal(claimed.Count, claimed.Distinct().Count());

        // null 的數量應該是 7 個 (10 個節點 - 3 筆訊息)
        var notClaimed = results.Where(x => !x.HasValue).Count();
        Assert.Equal(7, notClaimed);
    }
    /// <summary>
    /// 驗證領取訊息時會正確鎖定
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task ClaimMessage_Should_Lock_Message()
    {
        await SeedMessagesAsync();
        var repo = new MessageQueueRepository(_config);

        var msg1 = await repo.ClaimMessageAsync("NODE-1");

        // 檢查訊息狀態
        Assert.NotNull(msg1);// 是否有搶到訊息
        Assert.Equal("PROCESSING", msg1.STATUS); //狀態變成 PROCESSING
        Assert.Equal("NODE-1", msg1.LOCKED_BY); //鎖定者是 NODE-1
        Assert.NotNull(msg1.LOCKED_UNTIL); //有設定鎖定期限
    }
    /// <summary>
    /// 驗證標記完成功能
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task MarkDoneAsync_Should_Update_Status()
    {
        // Arrange
        await SeedMessagesAsync();
        var repo = new MessageQueueRepository(_config);
        var msg = await repo.ClaimMessageAsync("NODE-1");
        Assert.NotNull(msg);

        // 標記訊息完成
        await repo.MarkDoneAsync(msg.MESSAGE_ID);

        // 確認狀態
        using var conn = new SqlConnection(_connStr);
        var status = await conn.QueryFirstOrDefaultAsync<string>(
            "SELECT STATUS FROM MESSAGE_QUEUE WHERE MESSAGE_ID = @Id",
            new { Id = msg.MESSAGE_ID });

        //確認狀態變成DONE
        Assert.Equal("DONE", status);
    }

    /// <summary>
    ///  驗證計數功能的準確性
    /// </summary>
    /// <returns></returns>
    [Fact]
    public async Task GetPendingCountAsync_Should_Return_Correct_Count()
    {
        // Arrange
        await SeedMessagesAsync();
        var repo = new MessageQueueRepository(_config);

        var count = await repo.GetPendingCountAsync();

        // Assert
        Assert.Equal(3, count);

        await repo.ClaimMessageAsync("NODE-1");
        var countAfter = await repo.GetPendingCountAsync();
        //確認等待數量是否與預期相等
        Assert.Equal(2, countAfter);
    }
}