# 9mRoom 自定义状态块：DeepSeek 余额
# 用法：在自定义块中填写 ${& '<脚本绝对路径>'}
# API Key 顺序：环境变量 DEEPSEEK_API_KEY → %USERPROFILE%\.deepseek\api_key
# 注意：余额会随状态块同步到房间，请勿在公开房间使用。

$ErrorActionPreference = 'SilentlyContinue'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$key = $env:DEEPSEEK_API_KEY
if ([string]::IsNullOrWhiteSpace($key)) {
    $keyFile = Join-Path $HOME '.deepseek\api_key'
    if (Test-Path -LiteralPath $keyFile) {
        $key = (Get-Content -LiteralPath $keyFile -Raw).Trim()
    }
}

if ([string]::IsNullOrWhiteSpace($key)) {
    'DeepSeek 未配置 API Key'
    return
}

try {
    $headers = @{
        Authorization = "Bearer $key"
        Accept = 'application/json'
    }
    $response = Invoke-RestMethod -Uri 'https://api.deepseek.com/user/balance' `
        -Headers $headers -Method Get -TimeoutSec 2

    if (-not $response.is_available) {
        'DeepSeek 余额不可用'
        return
    }

    $balance = @($response.balance_infos) | Select-Object -First 1
    if ($null -eq $balance) {
        'DeepSeek 余额：无数据'
        return
    }

    $symbol = switch ($balance.currency) {
        'CNY' { '¥' }
        'USD' { '$' }
        default { [string]$balance.currency + ' ' }
    }

    $total = [decimal]$balance.total_balance
    $granted = [decimal]$balance.granted_balance
    $toppedUp = [decimal]$balance.topped_up_balance

    $totalText = $total.ToString('0.00', [Globalization.CultureInfo]::InvariantCulture)
    $grantedText = $granted.ToString('0.00', [Globalization.CultureInfo]::InvariantCulture)
    $toppedUpText = $toppedUp.ToString('0.00', [Globalization.CultureInfo]::InvariantCulture)

    "DeepSeek 余额 $symbol$totalText（赠送 $symbol$grantedText · 充值 $symbol$toppedUpText）"
}
catch {
    $status = $null
    if ($_.Exception.Response -and $_.Exception.Response.StatusCode) {
        $status = [int]$_.Exception.Response.StatusCode
    }

    if ($status -eq 401) {
        'DeepSeek API Key 无效或已过期'
    }
    elseif ($status -eq 429) {
        'DeepSeek 查询过于频繁'
    }
    elseif ($status) {
        "DeepSeek 查询失败（HTTP $status）"
    }
    else {
        'DeepSeek 查询失败：网络不可用'
    }
}
