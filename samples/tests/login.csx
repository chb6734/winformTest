// C# 스크립트 샘플. 실행: e2e run login.csx
// 글로벌: app (IApp)
app.MainWindow.Find("#txtUser").Fill("admin");
app.MainWindow.Find("#txtPassword").Fill("pass1234");
app.MainWindow.Find("#btnLogin").Click();
app.Wait(500);
app.MainWindow.Find("#lblStatus").ExpectText("환영합니다");
app.Screenshot("welcome");
