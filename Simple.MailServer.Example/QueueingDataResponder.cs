using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Simple.MailServer.Smtp;
using Simple.MailServer.Smtp.Config;

namespace Simple.MailServer.Example
{
    public class QueueingDataResponder : DefaultSmtpDataResponder<ISmtpServerConfiguration>
    {
        public QueueingDataResponder(ISmtpServerConfiguration configuration) : base(configuration)
        {
        }

        public override SmtpResponse DataStart(SmtpSessionInfo sessionInfo)
        {
            return base.DataStart(sessionInfo);
        }

        public override SmtpResponse DataLine(SmtpSessionInfo sessionInfo, byte[] lineBuf)
        {
            return base.DataLine(sessionInfo, lineBuf);
        }

        public override SmtpResponse DataEnd(SmtpSessionInfo sessionInfo)
        {
            return base.DataEnd(sessionInfo);
        }
    }
}
