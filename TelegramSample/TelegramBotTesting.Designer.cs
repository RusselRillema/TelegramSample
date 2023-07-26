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
            bnDelete = new Button();
            label1 = new Label();
            txtMessagId = new TextBox();
            btnEditButtons = new Button();
            lblChatId = new Label();
            txtReplyToMessageId = new TextBox();
            label2 = new Label();
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
            txtTextToSend.Location = new Point(321, 75);
            txtTextToSend.Multiline = true;
            txtTextToSend.Name = "txtTextToSend";
            txtTextToSend.Size = new Size(467, 200);
            txtTextToSend.TabIndex = 2;
            // 
            // btnSend
            // 
            btnSend.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
            btnSend.Location = new Point(321, 444);
            btnSend.Name = "btnSend";
            btnSend.Size = new Size(467, 36);
            btnSend.TabIndex = 3;
            btnSend.Text = "Send";
            btnSend.UseVisualStyleBackColor = true;
            btnSend.Click += btnSend_Click;
            // 
            // txtInlineButtons
            // 
            txtInlineButtons.Location = new Point(321, 302);
            txtInlineButtons.Multiline = true;
            txtInlineButtons.Name = "txtInlineButtons";
            txtInlineButtons.Size = new Size(219, 136);
            txtInlineButtons.TabIndex = 4;
            // 
            // lblText
            // 
            lblText.AutoSize = true;
            lblText.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
            lblText.Location = new Point(321, 51);
            lblText.Name = "lblText";
            lblText.Size = new Size(91, 21);
            lblText.TabIndex = 5;
            lblText.Text = "Text to send";
            // 
            // lblInlineButtons
            // 
            lblInlineButtons.AutoSize = true;
            lblInlineButtons.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
            lblInlineButtons.Location = new Point(321, 278);
            lblInlineButtons.Name = "lblInlineButtons";
            lblInlineButtons.Size = new Size(160, 21);
            lblInlineButtons.TabIndex = 6;
            lblInlineButtons.Text = "Inline buttons to send";
            // 
            // lblKeyboardButtons
            // 
            lblKeyboardButtons.AutoSize = true;
            lblKeyboardButtons.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
            lblKeyboardButtons.Location = new Point(569, 278);
            lblKeyboardButtons.Name = "lblKeyboardButtons";
            lblKeyboardButtons.Size = new Size(188, 21);
            lblKeyboardButtons.TabIndex = 8;
            lblKeyboardButtons.Text = "Keyboard buttons to send";
            // 
            // txtKeyboardButtons
            // 
            txtKeyboardButtons.Location = new Point(569, 302);
            txtKeyboardButtons.Multiline = true;
            txtKeyboardButtons.Name = "txtKeyboardButtons";
            txtKeyboardButtons.Size = new Size(219, 136);
            txtKeyboardButtons.TabIndex = 7;
            // 
            // chkClearInputsOnSend
            // 
            chkClearInputsOnSend.AutoSize = true;
            chkClearInputsOnSend.Location = new Point(321, 486);
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
            // bnDelete
            // 
            bnDelete.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
            bnDelete.Location = new Point(965, 444);
            bnDelete.Name = "bnDelete";
            bnDelete.Size = new Size(146, 36);
            bnDelete.TabIndex = 12;
            bnDelete.Text = "Delete Message";
            bnDelete.UseVisualStyleBackColor = true;
            bnDelete.Click += bnDelete_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
            label1.Location = new Point(832, 493);
            label1.Name = "label1";
            label1.Size = new Size(91, 21);
            label1.TabIndex = 13;
            label1.Text = "Message Id:";
            // 
            // txtMessagId
            // 
            txtMessagId.Location = new Point(929, 491);
            txtMessagId.Name = "txtMessagId";
            txtMessagId.Size = new Size(182, 23);
            txtMessagId.TabIndex = 14;
            // 
            // btnEditButtons
            // 
            btnEditButtons.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
            btnEditButtons.Location = new Point(832, 444);
            btnEditButtons.Name = "btnEditButtons";
            btnEditButtons.Size = new Size(127, 36);
            btnEditButtons.TabIndex = 15;
            btnEditButtons.Text = "Edit Buttons";
            btnEditButtons.UseVisualStyleBackColor = true;
            btnEditButtons.Click += btnEditButtons_Click;
            // 
            // lblChatId
            // 
            lblChatId.AutoSize = true;
            lblChatId.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
            lblChatId.Location = new Point(832, 9);
            lblChatId.Name = "lblChatId";
            lblChatId.Size = new Size(62, 21);
            lblChatId.TabIndex = 16;
            lblChatId.Text = "ChatId: ";
            // 
            // txtReplyToMessageId
            // 
            txtReplyToMessageId.Location = new Point(479, 32);
            txtReplyToMessageId.Name = "txtReplyToMessageId";
            txtReplyToMessageId.Size = new Size(309, 23);
            txtReplyToMessageId.TabIndex = 18;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Segoe UI", 12F, FontStyle.Regular, GraphicsUnit.Point);
            label2.Location = new Point(321, 30);
            label2.Name = "label2";
            label2.Size = new Size(152, 21);
            label2.TabIndex = 17;
            label2.Text = "Reply to message Id:";
            // 
            // TelegramBotTesting
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1136, 651);
            Controls.Add(txtReplyToMessageId);
            Controls.Add(label2);
            Controls.Add(lblChatId);
            Controls.Add(btnEditButtons);
            Controls.Add(txtMessagId);
            Controls.Add(label1);
            Controls.Add(bnDelete);
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
        private Button bnDelete;
        private Label label1;
        private TextBox txtMessagId;
        private Button btnEditButtons;
        private Label lblChatId;
        private TextBox txtReplyToMessageId;
        private Label label2;
    }
}