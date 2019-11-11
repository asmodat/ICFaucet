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
MUON_PROPS: {"denom":"muon","amount":50000,"index":118,"lcd":"https://lcd.gaia.bigdipper.live","gas":100000,"fees":50000}
```

> Function code

```
Code entry type -> .zip -> (execute ./publish.sh script to generate)
Runtime: .NET Core 2.1 (C#/PowerShell)
Handler: ICFaucet::ICFaucet.Function::FunctionHandler
```


## Get Tokens Example Use
> Join: `https://t.me/cosmosproject` and `https://t.me/kirainterex`
> Post a message: `Give me $MUON cosmos1k5wrdtmd5ngqx4pngwtlmlahv8yz7gk2tccgqg` on any of the chats

> Accepted Flags
```
--lcd=<url>
--network=<chain_id>
--index=<coin_index> //
--denom=<coin_denom>
```

> Example explicit message: `Give me $muon cosmos1k5wrdtmd5ngqx4pngwtlmlahv8yz7gk2tccgqg --index=118 --lcd=https://lcd.gaia.bigdipper.live`
> Example implicit message: `Give me $muon cosmos1k5wrdtmd5ngqx4pngwtlmlahv8yz7gk2tccgqg` will use default parameters

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




