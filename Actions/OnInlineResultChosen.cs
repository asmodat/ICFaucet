using System;
using System.Diagnostics;
using System.Threading.Tasks;
using AsmodatStandard.Extensions;
using AsmodatStandard.Extensions.Collections;
using Amazon.Lambda.Core;
using AsmodatStandard.Networking;
using Telegram.Bot;
using System.Collections.Generic;
using Telegram.Bot.Types;
using AsmodatStandard.Extensions.Threading;
using AsmodatStandard.Extensions.Security;
using AWSWrapper.SM;
using Newtonsoft.Json.Linq;
using System.Security;
using System.Threading;

namespace ICFaucet
{
    public partial class Function
    {
        
        private void _TBC_OnInlineResultChosen(object sender, Telegram.Bot.Args.ChosenInlineResultEventArgs e)
        {
            var userId = e.ChosenInlineResult.From.Id;





        }
    }
}
