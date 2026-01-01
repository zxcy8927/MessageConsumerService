using MessageConsumerService.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageConsumerService.Services
{
    public class MessageHandler
    {
        public async Task HandleAsync(MessageEntity msg)
        {
             throw new Exception("TEST FAILED");
            //await Task.Delay(500);

            //if (msg.MESSAGE_TYPE == "FAIL_TEST")
            //{
            //    throw new Exception("模擬處理失敗");
            //}
        }
    }

}
