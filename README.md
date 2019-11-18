# AWSLauncher

Deployment System Based on AWS Lambda, latest release can be found [here](https://github.com/asmodat/ICFaucet/releases).

## Installing Debug Tools

> https://github.com/emgdev/lambda-local-runner

```
dotnet tool install -g Amazon.Lambda.TestTool-2.1
dotnet tool update -g Amazon.Lambda.TestTool-2.1
```

Set env variable `AWS_PROFILE` to your credential profile on local. 


## Setup Lambda Function

> Wizard

```
Function name: ICFaucet
Runtime: .NET Core 2.1 (C#/PowerShell)
Choose or create an existing role -> existing role -> (create role with permissions to secrets and ec2)
```

> Add Trigger

```
CloudWatch Event
Rule -> Create new rule
	Rule name -> ICFaucet-Trigger
	Schedule expression -> rate(1 minute)
	Enable trigger -> yes
```

> ICFaucet

```
Memory: 256 MB
Timeout: 15 min
Network -> depending on your security requirements
```

> Environment variables

```
MASTER_CHAT_ID: <Int64>
MAX_PARALLELISM: 10
MAX_MESSAGE_AGE: 86400 //seconds
SECRET_NAME: <string> //secret manager, secret name
test_connection: true //test internet connection
<Token Name>_PROPS: { //custo
 "denom":"muon",
 "index":118,
 "lcd":"https://lcd.gaia.bigdipper.live",
 "amount":50000,
 "gas":100000,
 "fees":50000
}
```

> Props Examples
```
muon_PROPS: {"denom":"muon","amount":50000,"index":118,"prefix":"cosmos","lcd":"https://lcd.gaia.bigdipper.live","gas":100000,"fees":100}
cyb_PROPS: {"denom":"cyb","amount":50000,"index":118,"prefix":"cyber","lcd":"http://93.125.26.210:27117","gas":100000,"fees":100}
atom_PROPS: {"denom":"uatom","amount":50000,"index":118,"prefix":"cosmos","lcd":"https://lcd.nylira.net","gas":100000,"fees":100}
uatom_PROPS: {"denom":"uatom","amount":50000,"index":118,"prefix":"cosmos","lcd":"https://lcd.nylira.net","gas":100000,"fees":100}
tree_PROPS: {"denom":"utree","amount":50000,"index":118,"prefix":"xrn:","lcd":"https://regen-lcd.chorus.one:1317","gas":100000,"fees":100}
tsent_PROPS: {"denom":"tsent","amount":50000,"index":118,"prefix":"sent","lcd":"https://lcd.turing.dragonstake.io","gas":100000,"fees":100,"memo":"@InterchainWalletBot by KiraEx.com, LCD by DragonStake.io"}
tiris_PROPS: {"denom":"iris-atto","amount":50000,"index":118,"prefix":"faa","lcd":"http://iris_testnet.nodeateam.com:1317","gas":100000,"fees":50}
kava_PROPS: {"denom":"ukava","amount":50000,"index":118,"prefix":"kava","lcd":"https://lcd.kava.dragonstake.io","gas":100000,"fees":50, "memo":"@InterchainWalletBot by KiraEx.com, LCD by DragonStake.io"}
luna_PROPS: {"denom":"uluna","amount":50000,"index":118,"prefix":"terra","lcd":"https://lcd.terra.bigdipper.live","gas":100000,"fees":50}
uluna_PROPS: {"denom":"uluna","amount":50000,"index":118,"prefix":"terra","lcd":"https://lcd.terra.bigdipper.live","gas":100000,"fees":50}
x3ngm_PROPS: {"denom":"x3ngm","amount":10000,"index":118,"prefix":"emoney","lcd":"https://lilmermaid.validator.network/light","gas":100000,"fees":50}
x2teur_PROPS: {"denom":"x2eur","amount":10000,"index":118,"prefix":"emoney","lcd":"https://lilmermaid.validator.network/light","gas":100000,"fees":50}
x2tchf_PROPS: {"denom":"x2chf","amount":10000,"index":118,"prefix":"emoney","lcd":"https://lilmermaid.validator.network/light","gas":100000,"fees":50}
tcommercio_PROPS: {"denom":"ucommercio","amount":10000,"index":118,"prefix":"did:com:","lcd":"https://lcd-testnet.commercio.network","gas":100000,"fees":50}

```

> Exmplict Use Examples
```
tip 1 $TSENT --index=118 --prefix=sent --lcd=https://lcd.turing.dragonstake.io
show my $IRIS balance --denom=iris-atto --index=118 --prefix=faa --lcd=http://iris_testnet.nodeateam.com:1317
```
 
> Function code
```
Code entry type -> .zip -> (execute ./publish.sh script to generate)
Runtime: .NET Core 2.1 (C#/PowerShell)
Handler: ICFaucet::ICFaucet.Function::FunctionHandler
```

## Get Tokens Example Use
> Join: `https://t.me/cosmosproject` and `https://t.me/kirainterex`
> Post a message: `Give me $MUON` on any of the chats

> Accepted Flags
```
--lcd=<url>
--network=<chain_id>
--index=<coin_index> //
--denom=<coin_denom>
--address=<wallet_address>
```

> Example explicit message: `Give me $muon cosmos1k5wrdtmd5ngqx4pngwtlmlahv8yz7gk2tccgqg --index=118 --lcd=https://lcd.gaia.bigdipper.live`
> Example implicit message: `Give me $muon` will use default parameters

> Coin indexes can be found [here](https://github.com/satoshilabs/slips/blob/master/slip-0044.md)

## Get Deposit Address Example Use

> Join: `https://t.me/cosmosproject` and `https://t.me/kirainterex`
> Post a message: `Give me $MUON deposit` on any of the chats

> Accepted Flags
```
--prefix=<wallet_prefix>
--index=<coin_index>
```

> Example explicit message: `Give me $muon deposit --index=118 --prefix=cosmos`
> Example implicit message: `Give me $muon deposit` will use default parameters

## Transfer Tokens use example

> Reply to: `tip 1 $MUON --index=118 --prefix=cosmos --lcd=https://lcd.gaia.bigdipper.live`

## Netowrking

> Lambdas suffer from some networking issues especially when working with private subnets
> For testing it is recommended to use NoVPC, while on mainnet propper configuration of VPC, subnets, routes, gateway & nat along endpoints is required
> Following resources might be helpfull when dealing with the issues.

```
https://forums.aws.amazon.com/thread.jspa?threadID=279633
https://gist.github.com/reggi/dc5f2620b7b4f515e68e46255ac042a7
https://docs.aws.amazon.com/vpc/latest/userguide/vpc-dns.html
https://www.oodlestechnologies.com/blogs/How-to-grant-internet-access-to-AWS-Lambda-under-VPC/
```

## Bot Description

```
Kira Faucet allows you to claim tokens from any `cosmos-sdk` based project.

To receive tokens join @kirainterex and send following message:
`Give me $<token_name> <account_address> --index=<coin_index> --lcd=<lcd_url_address>`

To deposit tokens join @kirainterex and send following message:
`Give me $<token_name> deposit --index=<coin_index> --prefix=<wallet_prefix>`

To register new token ping @asmodat, and use faucet with implicit commands:
`Give me $<token_name> <account_address>`
`Give me $<token_name> deposit`
```

```
To receive tokens:
`Get me $<token_name> <account_address> --index=<coin_index> --lcd=<lcd_url_address>`

To deposit tokens:
`Get me $<token_name> deposit --index=<coin_index> --prefix=<wallet_prefix>`

To register new token ping @asmodat, and use faucet with implicit commands:
`Get me $<token_name> <account_address>`
`Get me $<token_name> deposit`
```



