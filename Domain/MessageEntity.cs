using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MessageConsumerService.Domain
{
    public class MessageEntity
    {
        public Guid MESSAGE_ID { get; set; }
        public string MESSAGE_TYPE { get; set; }
        public string PAYLOAD { get; set; }
        public string STATUS { get; set; }
        public int RETRY_COUNT { get; set; }
        public string LOCKED_BY { get; set; }
        public DateTime? LOCKED_UNTIL { get; set; }
    }

}
