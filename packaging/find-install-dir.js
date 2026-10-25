// 在升级安装前找出已安装 9mRoom 的实际目录。
// 依次尝试：正在运行的进程、开始菜单/桌面快捷方式、安装器写入的注册表位置。
// 失败时保持静默，让 MSI 使用默认目录。
try {
    var fso = new ActiveXObject("Scripting.FileSystemObject");
    var shell = new ActiveXObject("WScript.Shell");
    var candidates = [];

    function addDirectory(path) {
        try {
            if (!path) {
                return;
            }
            var directory = fso.GetParentFolderName(path);
            if (directory && fso.FolderExists(directory)) {
                candidates.push(directory);
            }
        } catch (ignored) {
        }
    }

    // 正在运行的客户端最准确。
    try {
        var wmi = GetObject("winmgmts:{impersonationLevel=impersonate}!//./root/cimv2");
        var processes = wmi.ExecQuery("SELECT ExecutablePath FROM Win32_Process WHERE Name='NineMRoom.Client.App.exe'");
        var iterator = new Enumerator(processes);
        if (!iterator.atEnd()) {
            addDirectory(iterator.item().ExecutablePath);
        }
    } catch (ignored) {
    }

    // 安装器创建的快捷方式。
    try {
        var startMenu = shell.SpecialFolders("CommonStartMenu");
        if (startMenu) {
            var link = shell.CreateShortcut(startMenu + "\\Programs\\9mRoom\\9mRoom.lnk");
            addDirectory(link.TargetPath);
        }
    } catch (ignored) {
    }

    try {
        var commonDesktop = shell.SpecialFolders("CommonDesktop");
        if (commonDesktop) {
            addDirectory(shell.CreateShortcut(commonDesktop + "\\9mRoom.lnk").TargetPath);
        }
    } catch (ignored) {
    }

    // 新版本安装器会写入该值，供后续升级直接使用。
    try {
        var registered = shell.RegRead("HKLM\\Software\\9mRoom\\InstallDir");
        if (registered && fso.FolderExists(registered)) {
            candidates.push(registered);
        }
    } catch (ignored) {
    }

    if (candidates.length > 0) {
        Session.Property("EXISTINGINSTALLFOLDER") = candidates[0];
    }
} catch (ignored) {
}
