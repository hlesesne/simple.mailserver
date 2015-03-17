﻿#region Header
// Copyright (c) 2013 Hans Wolff
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
#endregion

using Simple.MailServer.Logging;
using System;

namespace Simple.MailServer.Smtp
{
    internal class SmtpSessionInfoResponder : SmtpCommandParser
    {
        public SmtpSessionInfo SessionInfo { get; private set; }
        
        private readonly ISmtpResponderFactory _responderFactory;

        public SmtpSessionInfoResponder(ISmtpResponderFactory responderFactory, SmtpSessionInfo sessionInfo)
        {
            if (responderFactory == null) throw new ArgumentNullException("responderFactory");
            if (sessionInfo == null) throw new ArgumentNullException("sessionInfo");

            SessionInfo = sessionInfo;
            _responderFactory = responderFactory;
        }

        protected override SmtpResponse ProcessCommandDataStart(string name, string arguments)
        {
            // ReSharper disable PossibleUnintendedReferenceComparison
            var notIdentified = CreateResponseIfNotIdentified();
            if (notIdentified != SmtpResponse.None) return notIdentified;

            var hasNotMailFrom = CreateResponseIfHasNotMailFrom();
            if (hasNotMailFrom != SmtpResponse.None) return hasNotMailFrom;

            var hasNoRecipients = CreateResponseIfHasNoRecipients();
            if (hasNoRecipients != SmtpResponse.None) return hasNotMailFrom;
            // ReSharper restore PossibleUnintendedReferenceComparison

            var response = _responderFactory.DataResponder.DataStart(SessionInfo);

            if (response.Success)
            {
                InDataMode = true;
                SessionInfo.HasData = true;
            }

            return response;
        }

        protected override SmtpResponse ProcessCommandEhlo(string name, string arguments)
        {
            if (String.IsNullOrWhiteSpace(arguments))
            {
                return new SmtpResponse(501, "EHLO Missing domain address.");
            }

            var identification = new SmtpIdentification(SmtpIdentificationMode.EHLO, arguments);
            var response = _responderFactory.IdentificationResponder.VerifyIdentification(SessionInfo, identification);

            if (response.Success)
            {
                SessionInfo.Identification = identification;
            }

            return response;
        }

        protected override SmtpResponse ProcessCommandHelo(string name, string arguments)
        {
            if (String.IsNullOrWhiteSpace(arguments))
            {
                return new SmtpResponse(501, "HELO Missing domain address.");
            }

            var identification = new SmtpIdentification(SmtpIdentificationMode.HELO, arguments);
            var response = _responderFactory.IdentificationResponder.VerifyIdentification(SessionInfo, identification);

            if (response.Success)
            {
                SessionInfo.Identification = identification;
            }

            return response;
        }

        protected override SmtpResponse ProcessCommandMailFrom(string name, string arguments)
        {
            var notIdentified = CreateResponseIfNotIdentified();
            if (notIdentified != SmtpResponse.None) return notIdentified;

            var mailFrom = arguments.Trim();
            MailAddressWithParameters mailAddressWithParameters;
            try { mailAddressWithParameters = MailAddressWithParameters.Parse(mailFrom); }
            catch (FormatException)
            {
                return SmtpResponse.SyntaxError;
            }

            var response = _responderFactory.MailFromResponder.VerifyMailFrom(SessionInfo, mailAddressWithParameters);
            if (response.Success)
            {
                SessionInfo.MailFrom = mailAddressWithParameters;
            }

            return response;
        }

        protected override SmtpResponse ProcessCommandNoop(string name, string arguments)
        {
            return SmtpResponse.OK;
        }

        protected override SmtpResponse ProcessCommandQuit(string name, string arguments)
        {
            return SmtpResponse.Disconnect;
        }

        protected override SmtpResponse ProcessCommandRcptTo(string name, string arguments)
        {
            var notIdentified = CreateResponseIfNotIdentified();
            if (notIdentified != SmtpResponse.None) return notIdentified;

            var hasNotMailFrom = CreateResponseIfHasNotMailFrom();
            if (hasNotMailFrom != SmtpResponse.None) return hasNotMailFrom;

            var recipient = arguments.Trim();
            MailAddressWithParameters mailAddressWithParameters;
            try { mailAddressWithParameters = MailAddressWithParameters.Parse(recipient); }
            catch (FormatException)
            {
                return SmtpResponse.SyntaxError;
            }

            var response = _responderFactory.RecipientToResponder.VerifyRecipientTo(SessionInfo, mailAddressWithParameters);
            if (response.Success)
            {
                SessionInfo.Recipients.Add(mailAddressWithParameters);
            }

            return response;
        }

        protected override SmtpResponse ProcessCommandRset(string name, string arguments)
        {
            var response = _responderFactory.ResetResponder.Reset(SessionInfo);
            if (response.Success)
            {
                SessionInfo.Reset();
                InDataMode = false;
            }
            return response;
        }

        protected override SmtpResponse ProcessCommandVrfy(string name, string arguments)
        {
            var notIdentified = CreateResponseIfNotIdentified();
            if (notIdentified != SmtpResponse.None) return notIdentified;

            if (String.IsNullOrWhiteSpace(arguments))
            {
                return new SmtpResponse(501, "VRFY Missing parameter.");
            }

            var response = _responderFactory.VerifyResponder.Verify(SessionInfo, arguments);
            return response;
        }

        protected override SmtpResponse ProcessCommandAuth(string name, string value)
        {
            // TODO: Stick username provided in "value" parameter somewhere.
            // Response with 334, but will need another way to capture the Password being sent in the next transaction, which doesn't contain a command name parameter.
            return new SmtpResponse(334, value);
        }

        private SmtpResponse CreateResponseIfNotIdentified()
        {
            if (SessionInfo.Identification.Mode == SmtpIdentificationMode.NotIdentified)
            {
                return SmtpResponse.NotIdentified;
            }
            return SmtpResponse.None;
        }

        private SmtpResponse CreateResponseIfHasNotMailFrom()
        {
            if (SessionInfo.MailFrom == null)
            {
                return new SmtpResponse(502, "5.5.1 Use MAIL FROM first.");
            }
            return SmtpResponse.None;
        }

        private SmtpResponse CreateResponseIfHasNoRecipients()
        {
            if (SessionInfo.Recipients.Count <= 0)
            {
                return new SmtpResponse(503, "5.5.1 Must have recipient first");
            }
            return SmtpResponse.None;
        }

        protected override SmtpResponse ProcessRawLine(string line)
        {
            MailServerLogger.Instance.Debug("<<< " + line);

            var response = _responderFactory.RawLineResponder.RawLine(SessionInfo, line);
            return response;
        }

        protected override SmtpResponse ProcessCommandDataEnd()
        {
            MailServerLogger.Instance.Debug("DataEnd received"); 
            
            var response = _responderFactory.DataResponder.DataEnd(SessionInfo);
            if (response.Success)
            {
                InDataMode = false;
            }
            return response;
        }

        protected override SmtpResponse ProcessDataLine(byte[] line)
        {
            var response = _responderFactory.DataResponder.DataLine(SessionInfo, line);
            return response;
        }
    }
}
