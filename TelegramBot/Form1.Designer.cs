namespace TelegramBot
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            lblChatId = new Label();
            txtMessageIds = new TextBox();
            SuspendLayout();
            // 
            // lblChatId
            // 
            lblChatId.AutoSize = true;
            lblChatId.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
            lblChatId.Location = new Point(12, 9);
            lblChatId.Name = "lblChatId";
            lblChatId.Size = new Size(62, 21);
            lblChatId.TabIndex = 18;
            lblChatId.Text = "ChatId: ";
            // 
            // txtMessageIds
            // 
            txtMessageIds.Location = new Point(12, 33);
            txtMessageIds.Multiline = true;
            txtMessageIds.Name = "txtMessageIds";
            txtMessageIds.ReadOnly = true;
            txtMessageIds.Size = new Size(319, 405);
            txtMessageIds.TabIndex = 17;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(800, 450);
            Controls.Add(lblChatId);
            Controls.Add(txtMessageIds);
            Name = "Form1";
            Text = "Form1";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblChatId;
        private TextBox txtMessageIds;
    }
}