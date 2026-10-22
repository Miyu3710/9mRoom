$env:JAVA_HOME = 'D:\Projects\AndroidTools\java\jdk-17.0.20.1+1'
$env:ANDROID_HOME = 'D:\Projects\AndroidTools\sdk'
$env:ANDROID_SDK_ROOT = $env:ANDROID_HOME
$env:GRADLE_USER_HOME = 'D:\Projects\AndroidTools\gradle-cache'
$env:ANDROID_USER_HOME = 'D:\Projects\AndroidTools\android-user'
$env:Path = "$env:JAVA_HOME\bin;$env:ANDROID_HOME\platform-tools;$env:ANDROID_HOME\cmdline-tools\latest\bin;$env:Path"
