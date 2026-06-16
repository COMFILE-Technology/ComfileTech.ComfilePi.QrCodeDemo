namespace ComfileTech.ComfilePi.QrCodeDemo
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
            previewPictureBox = new PictureBox();
            statusLabel = new Label();
            repositoryLinkLabel = new LinkLabel();
            ((System.ComponentModel.ISupportInitialize)previewPictureBox).BeginInit();
            SuspendLayout();
            // 
            // previewPictureBox
            // 
            previewPictureBox.Location = new Point(0, 0);
            previewPictureBox.Name = "previewPictureBox";
            previewPictureBox.Size = new Size(480, 320);
            previewPictureBox.SizeMode = PictureBoxSizeMode.StretchImage;
            previewPictureBox.TabIndex = 0;
            previewPictureBox.TabStop = false;
            // 
            // statusLabel
            // 
            statusLabel.Location = new Point(0, 320);
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new Size(480, 50);
            statusLabel.TabIndex = 1;
            statusLabel.Text = "Waiting for QR...";
            statusLabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // repositoryLinkLabel
            // 
            repositoryLinkLabel.Location = new Point(0, 370);
            repositoryLinkLabel.Name = "repositoryLinkLabel";
            repositoryLinkLabel.Size = new Size(480, 30);
            repositoryLinkLabel.TabIndex = 2;
            repositoryLinkLabel.TabStop = true;
            repositoryLinkLabel.Text = RepositoryUrl;
            repositoryLinkLabel.TextAlign = ContentAlignment.MiddleCenter;
            repositoryLinkLabel.LinkClicked += RepositoryLinkLabel_LinkClicked;
            // 
            // Form1
            // 
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(480, 400);
            Controls.Add(repositoryLinkLabel);
            Controls.Add(statusLabel);
            Controls.Add(previewPictureBox);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            Name = "Form1";
            Text = "ComfilePi QR Code Demo";
            ((System.ComponentModel.ISupportInitialize)previewPictureBox).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private PictureBox previewPictureBox;
        private Label statusLabel;
        private LinkLabel repositoryLinkLabel;
    }
}
