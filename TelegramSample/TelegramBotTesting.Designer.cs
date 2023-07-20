namespace Sandwich.Sandbox
{
    partial class TelegramBotTesting
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            txtIncoming = new TextBox();
            lblIncoming = new Label();
            txtTextToSend = new TextBox();
            btnSend = new Button();
            txtInlineButtons = new TextBox();
            lblText = new Label();
            lblInlineButtons = new Label();
            lblKeyboardButtons = new Label();
            txtKeyboardButtons = new TextBox();
            chkClearInputsOnSend = new CheckBox();
            btnClearIncoming = new Button();
            txtMessageIds = new TextBox();
            SuspendLayout();
            // 
            // txtIncoming
            // 
            txtIncoming.Location = new Point(12, 33);
            txtIncoming.Multiline = true;
            txtIncoming.Name = "txtIncoming";
            txtIncoming.ReadOnly = true;
            txtIncoming.Size = new Size(279, 405);
            txtIncoming.TabIndex = 0;
            // 
            // lblIncoming
            // 
            lblIncoming.AutoSize = true;
            lblIncoming.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
            lblIncoming.Location = new Point(12, 9);
            lblIncoming.Name = "lblIncoming";
            lblIncoming.Size = new Size(75, 21);
            lblIncoming.TabIndex = 1;
            lblIncoming.Text = "Incoming";
            // 
            // txtTextToSend
            // 
            txtTextToSend.Location = new Point(320, 33);
            txtTextToSend.Multiline = true;
            txtTextToSend.Name = "txtTextToSend";
            txtTextToSend.Size = new Size(468, 200);
            txtTextToSend.TabIndex = 2;
            // 
            // btnSend
            // 
            btnSend.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
            btnSend.Location = new Point(320, 402);
            btnSend.Name = "btnSend";
            btnSend.Size = new Size(467, 36);
            btnSend.TabIndex = 3;
            btnSend.Text = "Send";
            btnSend.UseVisualStyleBackColor = true;
            btnSend.Click += btnSend_Click;
            // 
            // txtInlineButtons
            // 
            txtInlineButtons.Location = new Point(320, 260);
            txtInlineButtons.Multiline = true;
            txtInlineButtons.Name = "txtInlineButtons";
            txtInlineButtons.Size = new Size(219, 136);
            txtInlineButtons.TabIndex = 4;
            // 
            // lblText
            // 
            lblText.AutoSize = true;
            lblText.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
            lblText.Location = new Point(320, 9);
            lblText.Name = "lblText";
            lblText.Size = new Size(91, 21);
            lblText.TabIndex = 5;
            lblText.Text = "Text to send";
            // 
            // lblInlineButtons
            // 
            lblInlineButtons.AutoSize = true;
            lblInlineButtons.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
            lblInlineButtons.Location = new Point(320, 236);
            lblInlineButtons.Name = "lblInlineButtons";
            lblInlineButtons.Size = new Size(160, 21);
            lblInlineButtons.TabIndex = 6;
            lblInlineButtons.Text = "Inline buttons to send";
            // 
            // lblKeyboardButtons
            // 
            lblKeyboardButtons.AutoSize = true;
            lblKeyboardButtons.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
            lblKeyboardButtons.Location = new Point(568, 236);
            lblKeyboardButtons.Name = "lblKeyboardButtons";
            lblKeyboardButtons.Size = new Size(188, 21);
            lblKeyboardButtons.TabIndex = 8;
            lblKeyboardButtons.Text = "Keyboard buttons to send";
            // 
            // txtKeyboardButtons
            // 
            txtKeyboardButtons.Location = new Point(568, 260);
            txtKeyboardButtons.Multiline = true;
            txtKeyboardButtons.Name = "txtKeyboardButtons";
            txtKeyboardButtons.Size = new Size(219, 136);
            txtKeyboardButtons.TabIndex = 7;
            // 
            // chkClearInputsOnSend
            // 
            chkClearInputsOnSend.AutoSize = true;
            chkClearInputsOnSend.Location = new Point(320, 444);
            chkClearInputsOnSend.Name = "chkClearInputsOnSend";
            chkClearInputsOnSend.Size = new Size(135, 19);
            chkClearInputsOnSend.TabIndex = 9;
            chkClearInputsOnSend.Text = "Clear Inputs on Send";
            chkClearInputsOnSend.UseVisualStyleBackColor = true;
            // 
            // btnClearIncoming
            // 
            btnClearIncoming.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
            btnClearIncoming.Location = new Point(12, 449);
            btnClearIncoming.Name = "btnClearIncoming";
            btnClearIncoming.Size = new Size(279, 36);
            btnClearIncoming.TabIndex = 10;
            btnClearIncoming.Text = "Clear";
            btnClearIncoming.UseVisualStyleBackColor = true;
            btnClearIncoming.Click += btnClearIncoming_Click;
            // 
            // txtMessageIds
            // 
            txtMessageIds.Location = new Point(832, 33);
            txtMessageIds.Multiline = true;
            txtMessageIds.Name = "txtMessageIds";
            txtMessageIds.ReadOnly = true;
            txtMessageIds.Size = new Size(279, 405);
            txtMessageIds.TabIndex = 11;
            // 
            // TelegramBotTesting
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1136, 543);
            Controls.Add(txtMessageIds);
            Controls.Add(btnClearIncoming);
            Controls.Add(chkClearInputsOnSend);
            Controls.Add(lblKeyboardButtons);
            Controls.Add(txtKeyboardButtons);
            Controls.Add(lblInlineButtons);
            Controls.Add(lblText);
            Controls.Add(txtInlineButtons);
            Controls.Add(btnSend);
            Controls.Add(txtTextToSend);
            Controls.Add(lblIncoming);
            Controls.Add(txtIncoming);
            Name = "TelegramBotTesting";
            Text = "TelegramBotTesting";
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox txtIncoming;
        private Label lblIncoming;
        private TextBox txtTextToSend;
        private Button btnSend;
        private TextBox txtInlineButtons;
        private Label lblText;
        private Label lblInlineButtons;
        private Label lblKeyboardButtons;
        private TextBox txtKeyboardButtons;
        private CheckBox chkClearInputsOnSend;
        private Button btnClearIncoming;
        private TextBox txtMessageIds;
    }
}