namespace SCT_Updater
{
    partial class Form1
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
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.dgvModules = new System.Windows.Forms.DataGridView();
            this.cmsRowMenu = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.tsmiReinstall = new System.Windows.Forms.ToolStripMenuItem();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.lblStatus = new System.Windows.Forms.Label();
            this.btnCheckUpdates = new System.Windows.Forms.Button();
            this.btnUpdateAll = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.dgvModules)).BeginInit();
            this.SuspendLayout();
            // 
            // dgvModules
            // 
            this.dgvModules.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dgvModules.ContextMenuStrip = this.cmsRowMenu;
            this.dgvModules.Location = new System.Drawing.Point(12, 12);
            this.dgvModules.Name = "dgvModules";
            this.dgvModules.RowHeadersWidth = 51;
            this.dgvModules.Size = new System.Drawing.Size(776, 182);
            this.dgvModules.TabIndex = 0;
            this.dgvModules.CellContentClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.dgvModules_CellContentClick);
            // 
            // cmsRowMenu
            // 
            this.cmsRowMenu.ImageScalingSize = new System.Drawing.Size(20, 20);
            this.cmsRowMenu.Name = "cmsRowMenu";
            this.cmsRowMenu.Size = new System.Drawing.Size(61, 4);
            // 
            // tsmiReinstall
            // 
            this.tsmiReinstall.Name = "tsmiReinstall";
            this.tsmiReinstall.Size = new System.Drawing.Size(32, 19);
            // 
            // progressBar
            // 
            this.progressBar.Location = new System.Drawing.Point(12, 244);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(776, 23);
            this.progressBar.TabIndex = 1;
            // 
            // lblStatus
            // 
            this.lblStatus.AutoSize = true;
            this.lblStatus.Location = new System.Drawing.Point(12, 210);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(44, 16);
            this.lblStatus.TabIndex = 2;
            this.lblStatus.Text = "Status";
            // 
            // btnCheckUpdates
            // 
            this.btnCheckUpdates.Location = new System.Drawing.Point(15, 282);
            this.btnCheckUpdates.Name = "btnCheckUpdates";
            this.btnCheckUpdates.Size = new System.Drawing.Size(145, 39);
            this.btnCheckUpdates.TabIndex = 3;
            this.btnCheckUpdates.Text = "Check updates";
            this.btnCheckUpdates.UseVisualStyleBackColor = true;
            this.btnCheckUpdates.Click += new System.EventHandler(this.btnCheckUpdates_Click);
            // 
            // btnUpdateAll
            // 
            this.btnUpdateAll.Location = new System.Drawing.Point(181, 282);
            this.btnUpdateAll.Name = "btnUpdateAll";
            this.btnUpdateAll.Size = new System.Drawing.Size(145, 39);
            this.btnUpdateAll.TabIndex = 4;
            this.btnUpdateAll.Text = "Update all";
            this.btnUpdateAll.UseVisualStyleBackColor = true;
            this.btnUpdateAll.Click += new System.EventHandler(this.btnUpdateAll_Click);
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.ButtonHighlight;
            this.ClientSize = new System.Drawing.Size(807, 337);
            this.Controls.Add(this.btnUpdateAll);
            this.Controls.Add(this.btnCheckUpdates);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.dgvModules);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Name = "Form1";
            this.Text = "Updater";
            this.Load += new System.EventHandler(this.Form1_Load);
            ((System.ComponentModel.ISupportInitialize)(this.dgvModules)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.DataGridView dgvModules;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Button btnCheckUpdates;
        private System.Windows.Forms.ContextMenuStrip cmsRowMenu;
        private System.Windows.Forms.ToolStripMenuItem tsmiReinstall;
        private System.Windows.Forms.Button btnUpdateAll;
    }
}

