namespace ETACO.CommonUtils
{
    partial class SplashForm
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
            this.panel1 = new System.Windows.Forms.Panel();
            this.panel2 = new System.Windows.Forms.Panel();
            this.Logo = new System.Windows.Forms.PictureBox();
            this.labelProgress = new ETACO.CommonUtils.SmoothLabel();
            this.labelVersion = new ETACO.CommonUtils.SmoothLabel();
            this.labelAppName = new ETACO.CommonUtils.SmoothLabel();
            this.labelCopyRight = new ETACO.CommonUtils.SmoothLabel();
            this.labelSysName = new ETACO.CommonUtils.SmoothLabel();
            this.labelCopyRight2 = new ETACO.CommonUtils.SmoothLabel();
            this.panel1.SuspendLayout();
            this.panel2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.Logo)).BeginInit();
            this.SuspendLayout();
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.WhiteSmoke;
            this.panel1.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panel1.Controls.Add(this.labelProgress);
            this.panel1.Controls.Add(this.labelVersion);
            this.panel1.Controls.Add(this.labelAppName);
            this.panel1.Controls.Add(this.labelCopyRight);
            this.panel1.Controls.Add(this.panel2);
            this.panel1.Controls.Add(this.labelCopyRight2);
            this.panel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(462, 199);
            this.panel1.TabIndex = 12;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.labelSysName);
            this.panel2.Controls.Add(this.Logo);
            this.panel2.Dock = System.Windows.Forms.DockStyle.Top;
            this.panel2.Location = new System.Drawing.Point(0, 0);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(460, 72);
            this.panel2.TabIndex = 18;
            // 
            // Logo
            // 
            this.Logo.BackColor = System.Drawing.Color.Transparent;
            this.Logo.BackgroundImageLayout = System.Windows.Forms.ImageLayout.None;
            this.Logo.Dock = System.Windows.Forms.DockStyle.Left;
            this.Logo.Image = global::ETACO.CommonUtils.Properties.Resources.ETA_Logo;
            this.Logo.Location = new System.Drawing.Point(0, 0);
            this.Logo.Name = "Logo";
            this.Logo.Padding = new System.Windows.Forms.Padding(3, 3, 0, 0);
            this.Logo.Size = new System.Drawing.Size(155, 72);
            this.Logo.TabIndex = 17;
            this.Logo.TabStop = false;
            // 
            // labelProgress
            // 
            this.labelProgress.BackColor = System.Drawing.Color.Transparent;
            this.labelProgress.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelProgress.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.labelProgress.ForeColor = System.Drawing.Color.RoyalBlue;
            this.labelProgress.Location = new System.Drawing.Point(0, 132);
            this.labelProgress.Name = "labelProgress";
            this.labelProgress.Padding = new System.Windows.Forms.Padding(5, 0, 10, 2);
            this.labelProgress.Size = new System.Drawing.Size(460, 38);
            this.labelProgress.TabIndex = 16;
            this.labelProgress.Text = "Progress message ...";
            this.labelProgress.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            // 
            // labelVersion
            // 
            this.labelVersion.BackColor = System.Drawing.Color.Transparent;
            this.labelVersion.Dock = System.Windows.Forms.DockStyle.Top;
            this.labelVersion.Font = new System.Drawing.Font("Arial", 12F, System.Drawing.FontStyle.Bold);
            this.labelVersion.ForeColor = System.Drawing.Color.RoyalBlue;
            this.labelVersion.Location = new System.Drawing.Point(0, 108);
            this.labelVersion.Name = "labelVersion";
            this.labelVersion.Padding = new System.Windows.Forms.Padding(10, 0, 5, 0);
            this.labelVersion.Size = new System.Drawing.Size(460, 24);
            this.labelVersion.TabIndex = 14;
            this.labelVersion.Text = "ver X.Y.Z";
            this.labelVersion.TextAlign = System.Drawing.ContentAlignment.TopRight;
            this.labelVersion.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            // 
            // labelAppName
            // 
            this.labelAppName.BackColor = System.Drawing.Color.Transparent;
            this.labelAppName.Dock = System.Windows.Forms.DockStyle.Top;
            this.labelAppName.Font = new System.Drawing.Font("Arial", 15.75F, System.Drawing.FontStyle.Bold);
            this.labelAppName.ForeColor = System.Drawing.Color.RoyalBlue;
            this.labelAppName.Location = new System.Drawing.Point(0, 72);
            this.labelAppName.Name = "labelAppName";
            this.labelAppName.Padding = new System.Windows.Forms.Padding(10, 10, 5, 0);
            this.labelAppName.Size = new System.Drawing.Size(460, 36);
            this.labelAppName.TabIndex = 12;
            this.labelAppName.Text = "Application Name";
            this.labelAppName.TextAlign = System.Drawing.ContentAlignment.BottomRight;
            this.labelAppName.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            // 
            // labelCopyRight
            // 
            this.labelCopyRight.BackColor = System.Drawing.Color.Transparent;
            this.labelCopyRight.Dock = System.Windows.Forms.DockStyle.Bottom;
            this.labelCopyRight.Font = new System.Drawing.Font("Tahoma", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.labelCopyRight.ForeColor = System.Drawing.Color.DimGray;
            this.labelCopyRight.Location = new System.Drawing.Point(0, 170);
            this.labelCopyRight.Name = "labelCopyRight";
            this.labelCopyRight.Padding = new System.Windows.Forms.Padding(5, 0, 5, 7);
            this.labelCopyRight.Size = new System.Drawing.Size(460, 27);
            this.labelCopyRight.TabIndex = 13;
            this.labelCopyRight.Text = "Electronic Trade Agency && Co, 2015.";
            this.labelCopyRight.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            this.labelCopyRight.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            this.labelCopyRight.MouseClick += new System.Windows.Forms.MouseEventHandler(this.labelCopyRight_MouseClick);
            this.labelCopyRight.MouseEnter += new System.EventHandler(this.labelCopyRight_MouseEnter);
            this.labelCopyRight.MouseLeave += new System.EventHandler(this.labelCopyRight_MouseLeave);
            // 
            // labelSysName
            // 
            this.labelSysName.BackColor = System.Drawing.Color.Transparent;
            this.labelSysName.Dock = System.Windows.Forms.DockStyle.Fill;
            this.labelSysName.Font = new System.Drawing.Font("Tahoma", 27.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.labelSysName.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(42)))), ((int)(((byte)(74)))), ((int)(((byte)(155)))));
            this.labelSysName.Location = new System.Drawing.Point(155, 0);
            this.labelSysName.Name = "labelSysName";
            this.labelSysName.Padding = new System.Windows.Forms.Padding(5, 5, 5, 0);
            this.labelSysName.Size = new System.Drawing.Size(305, 72);
            this.labelSysName.TabIndex = 11;
            this.labelSysName.Text = "Sys Имя";
            this.labelSysName.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.labelSysName.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            // 
            // labelCopyRight2
            // 
            this.labelCopyRight2.BackColor = System.Drawing.Color.Transparent;
            this.labelCopyRight2.Font = new System.Drawing.Font("Tahoma", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.labelCopyRight2.ForeColor = System.Drawing.Color.MediumBlue;
            this.labelCopyRight2.Location = new System.Drawing.Point(2, 74);
            this.labelCopyRight2.Name = "labelCopyRight2";
            this.labelCopyRight2.Padding = new System.Windows.Forms.Padding(10, 10, 10, 0);
            this.labelCopyRight2.Size = new System.Drawing.Size(462, 98);
            this.labelCopyRight2.TabIndex = 15;
            this.labelCopyRight2.Text = "...";
            this.labelCopyRight2.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
            this.labelCopyRight2.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            this.labelCopyRight2.Visible = false;
            this.labelCopyRight2.MouseClick += new System.Windows.Forms.MouseEventHandler(this.labelCopyRight_MouseClick);
            // 
            // SplashForm
            // 
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.None;
            this.BackColor = System.Drawing.SystemColors.ButtonFace;
            this.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Zoom;
            this.ClientSize = new System.Drawing.Size(462, 199);
            this.ControlBox = false;
            this.Controls.Add(this.panel1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "SplashForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.TransparencyKey = System.Drawing.Color.Magenta;
            this.panel1.ResumeLayout(false);
            this.panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.Logo)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Panel panel1;
        private SmoothLabel labelVersion;
        private SmoothLabel labelAppName;
        private SmoothLabel labelCopyRight;
        private SmoothLabel labelSysName;
        private SmoothLabel labelCopyRight2;
        private SmoothLabel labelProgress;
        private System.Windows.Forms.PictureBox Logo;
        private System.Windows.Forms.Panel panel2;
    }
}