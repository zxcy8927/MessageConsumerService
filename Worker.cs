using MessageConsumerService.Repository;
using MessageConsumerService.Services;

namespace MessageConsumerService
{
    public class Worker : BackgroundService
    {
        private readonly MessageQueueRepository _repo;
        private readonly MessageHandler _handler;
        private readonly ILogger<Worker> _logger;
        private readonly string _nodeName = Environment.MachineName;

        private int _totalProcessed = 0;
        private int _successCount = 0;
        private int _failedCount = 0;
        private DateTime _startTime;

        public Worker(
            MessageQueueRepository repo,
            MessageHandler handler,
            ILogger<Worker> logger)
        {
            _repo = repo;
            _handler = handler;
            _logger = logger;
            _startTime = DateTime.Now;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var msg = await _repo.ClaimMessageAsync(_nodeName);

                if (msg == null)
                {
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }

                try
                {
                    await _handler.HandleAsync(msg);
                    await _repo.MarkDoneAsync(msg.MESSAGE_ID);

                    _successCount++;
                }
                catch (Exception ex)
                {
                    _failedCount++;
                    await _repo.ProcessFailedAsync(msg, ex.ToString());
                }
                finally
                {
                    _totalProcessed++;
                }

                await Task.Delay(50, stoppingToken);
            }
        }


        // 服務停止時的最終統計
        //var totalElapsed = DateTime.Now - _startTime;
        //_logger.LogInformation(
        //    $"[{_nodeName}] Worker 停止 | " +
        //    $"總執行時間: {totalElapsed:hh\\:mm\\:ss} | " +
        //    $"總處理: {_totalProcessed} 筆 (成功: {_successCount}, 失敗: {_failedCount})");
    }
}
