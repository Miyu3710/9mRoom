using System.Net;

namespace NineMRoom.Server;

public static class AdminPage
{
    private const string Style = """
        *{box-sizing:border-box}
        body{margin:0;background:#111114;color:#ececf0;font:14px/1.5 "Segoe UI","Microsoft YaHei UI",sans-serif}
        main{max-width:980px;margin:48px auto;padding:0 24px}
        h1{font-size:26px;margin:0 0 4px}
        h2{font-size:18px;margin:0 0 12px}
        p.muted{color:#9b9ba4;margin:0 0 24px}
        .card{background:#1b1b20;border:1px solid #303038;border-radius:12px;padding:20px;margin-bottom:18px}
        label{display:block;color:#9b9ba4;font-size:12px;margin:0 0 4px}
        input{width:100%;background:#25252b;border:1px solid #383842;color:#ececf0;border-radius:8px;padding:9px 10px;margin:0 0 12px}
        button{background:#6d7cff;color:#fff;border:0;border-radius:8px;padding:9px 16px;cursor:pointer}
        button.danger{background:#c94b4b;padding:6px 10px}
        button.ghost{background:#2b2b33;padding:6px 10px}
        .row{display:grid;grid-template-columns:1fr 1fr auto;gap:12px;align-items:end}
        table{width:100%;border-collapse:collapse}
        th,td{text-align:left;padding:10px 8px;border-bottom:1px solid #2c2c34}
        th{color:#9b9ba4;font-weight:400;font-size:12px}
        .tag{display:inline-block;background:#292934;border-radius:6px;padding:2px 7px;font-size:12px;color:#c9c9d2}
        .error{color:#ff8b8b;margin:0 0 12px}
        .actions{display:flex;gap:8px;align-items:center}
        .admin-actions{display:flex;gap:8px;align-items:center;flex-wrap:wrap}
        .admin-actions input{margin:0;width:150px}
        .login{max-width:380px;margin:120px auto}
        """;

    public static string Login(string? error = null) => $"""
        <!doctype html><html lang="zh-CN"><head><meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <title>9mRoom 管理员登录</title><style>{Style}</style></head><body>
        <main class="login"><div class="card">
        <h1>9mRoom 管理</h1><p class="muted">登录后管理服务端房间。</p>
        {(string.IsNullOrEmpty(error) ? string.Empty : $"<p class=\"error\">{WebUtility.HtmlEncode(error)}</p>")}
        <form method="post" action="/admin/login">
        <label>用户名</label><input name="username" autocomplete="username" autofocus>
        <label>密码</label><input name="password" type="password" autocomplete="current-password">
        <button type="submit">登录</button>
        </form></div></main></body></html>
        """;

    public static string Rooms(
        AdminRecord admin,
        IReadOnlyList<RoomRecord> rooms,
        Func<string, int> onlineCount,
        IReadOnlyList<AdminRecord> admins,
        string? error = null) => $"""
        <!doctype html><html lang="zh-CN"><head><meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <title>9mRoom 房间管理</title><style>{Style}</style></head><body>
        <main>
        <div class="actions" style="justify-content:space-between;margin-bottom:24px">
          <div><h1>房间管理</h1><p class="muted" style="margin:0">管理员：{WebUtility.HtmlEncode(admin.Username)}（{(admin.IsSuper ? "超级管理员" : "普通管理员")}）</p></div>
          <form method="post" action="/admin/logout"><button class="ghost" type="submit">退出登录</button></form>
        </div>
        {(string.IsNullOrEmpty(error) ? string.Empty : $"<p class=\"error\">{WebUtility.HtmlEncode(error)}</p>")}
        <div class="card"><h2>新建房间</h2>
          <form method="post" action="/admin/rooms/create">
            <div class="row">
              <div><label>房间码（小写字母 / 数字 / -）</label><input name="code" maxlength="64" placeholder="study-1" required></div>
              <div><label>显示名称</label><input name="name" maxlength="40" placeholder="晚间自习室" required></div>
              <button type="submit">创建</button>
            </div>
          </form>
        </div>
        <div class="card"><h2>房间列表</h2>
        <table><thead><tr><th>房间码</th><th>名称</th><th>类型</th><th>在线</th><th>创建时间</th><th></th></tr></thead><tbody>
        {string.Join(Environment.NewLine, rooms.Select(room => $"""
          <tr>
            <td><code>{WebUtility.HtmlEncode(room.Code)}</code></td>
            <td>{WebUtility.HtmlEncode(room.Name)}</td>
            <td><span class="tag">{(room.Kind == "server" ? "服务端房间" : "用户房间")}</span></td>
            <td>{onlineCount(room.Code)}</td>
            <td>{WebUtility.HtmlEncode(room.CreatedAt)}</td>
            <td><form method="post" action="/admin/rooms/delete" onsubmit="return confirm('确认删除房间？');">
              <input type="hidden" name="code" value="{WebUtility.HtmlEncode(room.Code)}">
              <button class="danger" type="submit">删除</button>
            </form></td>
          </tr>
          """))}
        </tbody></table>
        {(rooms.Count == 0 ? "<p class=\"muted\">还没有房间。</p>" : string.Empty)}
        </div>
        {(admin.IsSuper ? $"""
        <div class="card"><h2>管理员账号</h2>
          <form method="post" action="/admin/admins/create">
            <div class="row">
              <div><label>用户名</label><input name="username" maxlength="32" autocomplete="off" required></div>
              <div><label>密码（至少 8 位）</label><input name="password" type="password" minlength="8" maxlength="128" autocomplete="new-password" required></div>
              <button type="submit">创建普通管理员</button>
            </div>
          </form>
          <table><thead><tr><th>用户名</th><th>角色</th><th>操作</th></tr></thead><tbody>
          {string.Join(Environment.NewLine, admins.Select(item => $"""
            <tr>
              <td><code>{WebUtility.HtmlEncode(item.Username)}</code></td>
              <td><span class="tag">{(item.IsSuper ? "超级管理员" : "普通管理员")}</span></td>
              <td><div class="admin-actions">
                <form method="post" action="/admin/admins/password">
                  <input type="hidden" name="id" value="{item.Id}">
                  <input name="password" type="password" minlength="8" maxlength="128" placeholder="新密码" autocomplete="new-password" required>
                  <button class="ghost" type="submit">改密</button>
                </form>
                {(item.IsSuper ? string.Empty : $"""
                <form method="post" action="/admin/admins/delete" onsubmit="return confirm('确认删除管理员 {WebUtility.HtmlEncode(item.Username)}？');">
                  <input type="hidden" name="id" value="{item.Id}">
                  <button class="danger" type="submit">删除</button>
                </form>
                """)}
              </div></td>
            </tr>
            """))}
          </tbody></table>
          <p class="muted" style="margin:12px 0 0">超级管理员由服务器首次启动时的环境变量创建，不能在管理页删除。</p>
        </div>
        """ : string.Empty)}
        </main></body></html>
        """;
}
