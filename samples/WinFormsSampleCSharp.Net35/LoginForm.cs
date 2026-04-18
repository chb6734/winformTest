using System;
using System.Drawing;
using System.Windows.Forms;

namespace SampleWinFormsNet35
{
    public sealed class LoginForm : Form
    {
        private TextBox _user;
        private TextBox _pwd;
        private Button _login;
        private Label _status;

        public LoginForm()
        {
            Text = "로그인 (Net35)";
            Width = 360;
            Height = 260;
            StartPosition = FormStartPosition.CenterScreen;

            Label lblUser = new Label { Text = "사용자명", Location = new Point(30, 30), Width = 80 };
            _user = new TextBox { Location = new Point(120, 28), Width = 180, Name = "txtUser", AccessibleName = "사용자명" };
            Label lblPwd = new Label { Text = "비밀번호", Location = new Point(30, 70), Width = 80 };
            _pwd = new TextBox { Location = new Point(120, 68), Width = 180, UseSystemPasswordChar = true, Name = "txtPassword", AccessibleName = "비밀번호" };
            _login = new Button { Text = "로그인", Location = new Point(120, 110), Width = 80, Name = "btnLogin", AccessibleName = "로그인" };
            _login.Click += new EventHandler(OnLogin);
            _status = new Label { Location = new Point(30, 160), Width = 290, Height = 40, Name = "lblStatus", AccessibleName = "상태" };

            Controls.Add(lblUser); Controls.Add(_user);
            Controls.Add(lblPwd); Controls.Add(_pwd);
            Controls.Add(_login); Controls.Add(_status);
        }

        private void OnLogin(object sender, EventArgs e)
        {
            if (_user.Text == "admin" && _pwd.Text == "pass1234")
                _status.Text = "환영합니다, " + _user.Text + "님";
            else
                _status.Text = "로그인 실패";
        }
    }
}
