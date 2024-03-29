﻿using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Rowan.TfsWorkingOn.WinForm.Properties;

namespace Rowan.TfsWorkingOn.WinForm
{
    public partial class FormWorkItemConfiguration : Form
    {
        private WorkingItemConfiguration _workingItemConfiguration = new WorkingItemConfiguration();
        private dynamic _queryPickerControl;
        private Type _queryPickerControlType;

        public FormWorkItemConfiguration()
        {
            InitializeComponent();
        }

        private void FormWorkItemConfiguration_Load(object sender, EventArgs e)
        {
            workingItemConfigurationBindingSource.DataSource = _workingItemConfiguration;
            comboBoxWorkItemType.DataSource = _workingItemConfiguration.WorkItemTypes.ToList();
            comboBoxWorkItemType.DisplayMember = "Name";
            comboBoxWorkItemType.ValueMember = "Name";
            settingsBindingSource.DataSource = Settings.Default;

            _queryPickerControl = Activator.CreateInstance("Microsoft.TeamFoundation.WorkItemTracking.Controls, Version=15.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", "Microsoft.TeamFoundation.WorkItemTracking.Controls.QueryPickerControl").Unwrap();
            tabPageOptions.Controls.Add(_queryPickerControl);
            _queryPickerControl.Location = new System.Drawing.Point(91, 123);
            _queryPickerControl.AutoSize = false;
            _queryPickerControl.Size = new System.Drawing.Size(240, 21);
            _queryPickerControl.TabIndex = 21;
            // Can't use QueryPickerControl Methods or Properties with dynamic, since they are from an "internal" type. Only the "public" base type is exposed.
            // http://www.heartysoft.com/post/2010/05/26/anonymous-types-c-sharp-4-dynamic.aspx
            _queryPickerControlType = ((object)_queryPickerControl).GetType();
            //_queryPickerControl.Initialize(Connection.GetConnection().SelectedProject, null, 0);
            var wiType =
                Type.GetType(
                    "Microsoft.TeamFoundation.WorkItemTracking.Controls.QueryPickerType, Microsoft.TeamFoundation.WorkItemTracking.Controls, Version=15.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            var method = _queryPickerControlType.GetMethod("Initialize", new[] { typeof(Project), typeof(QueryItem), wiType });
            if (method != null) method.Invoke(_queryPickerControl, new object[] { Connection.GetConnection().SelectedProject, null, 0 });
            //_queryPickerControl.SelectedItemId = Settings.Default.SelectedQuery;
            try
            {
                _queryPickerControlType.GetProperty("SelectedItemId").SetValue(_queryPickerControl, Settings.Default.LastProjectCollectionWorkedOn.LastProjectWorkedOn.LastQueryWorkedOn.Value, null);
            }
            catch (Exception)
            {
                Settings.Default.LastProjectCollectionWorkedOn.LastProjectWorkedOn.LastQueryWorkedOn = null;
            }

            //_queryPickerControl.SelectedQueryItemChanged += new EventHandler(QueryPickerControl_OnSelectedQueryItemChanged);
            //Type eventHandlerType = Type.GetType("Microsoft.TeamFoundation.Controls.SelectedQueryItemChangedEventHandler, Microsoft.TeamFoundation.Common.Library, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            var eventInfo = _queryPickerControlType.GetEvent("SelectedQueryItemChanged", BindingFlags.Instance | BindingFlags.Public);
            if (eventInfo != null)
            {
                eventInfo.AddEventHandler(_queryPickerControl, Create(eventInfo, QueryPickerControl_OnSelectedQueryItemChanged));
            }

            toolTipHelp.SetToolTip(pictureBoxHelpUserActivity, Resources.HelpActivityMonitor);
            toolTipHelp.SetToolTip(pictureBoxHelpPromptOnResume, Resources.HelpPromptOnResume);
            toolTipHelp.SetToolTip(pictureBoxHelpNag, Resources.HelpNag);
            toolTipHelp.SetToolTip(pictureBoxHelpMenuQuery, Resources.HelpMenuQuery);

            var assemblyInformationalVersionAttribute = Assembly.GetExecutingAssembly().GetCustomAttributes(true).OfType<AssemblyInformationalVersionAttribute>().FirstOrDefault();
            if (
                assemblyInformationalVersionAttribute != null)
            {
                labelVersion.Text = assemblyInformationalVersionAttribute.InformationalVersion;
            }
        }

        // http://stackoverflow.com/questions/45779/c-dynamic-event-subscription
        private static Delegate Create(EventInfo evt, Action d)
        {
            var handlerType = evt.EventHandlerType;
            var eventParams = handlerType.GetMethod("Invoke").GetParameters();
            //lambda: (object x0, EventArgs x1) => d()      
            var parameters = eventParams.Select(p => Expression.Parameter(p.ParameterType, "x"));
            // - assumes void method with no arguments but can be        
            //   changed to accommodate any supplied method      
            var body = Expression.Call(Expression.Constant(d), d.GetType().GetMethod("Invoke"));
            var lambda = Expression.Lambda(body, parameters.ToArray());
            return Delegate.CreateDelegate(handlerType, lambda.Compile(), "Invoke", false);
        }

        protected void QueryPickerControl_OnSelectedQueryItemChanged()
        {
            Settings.Default.LastProjectCollectionWorkedOn.LastProjectWorkedOn.LastQueryWorkedOn = _queryPickerControlType.GetProperty("SelectedItemId").GetValue(_queryPickerControl, null);
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            buttonSave_Click(sender, e);
            Close();
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            _workingItemConfiguration.Save();
            Settings.Default.Save();
        }

        private void buttonSetDirectory_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog.ShowDialog(this) == DialogResult.OK)
            {
                Settings.Default.ConfigurationsPath = folderBrowserDialog.SelectedPath;
            }
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            Close();
        }

        private void FormWorkItemConfiguration_FormClosing(object sender, FormClosingEventArgs e)
        {
            // TODO: Settings.Default.IsDirty is set to dirty when you press cancel?!?!

            if (Settings.Default.IsDirty || _workingItemConfiguration.IsDirty)
            {
                DialogResult result = MessageBox.Show(Resources.OutstandingChanges, Resources.SaveChanges, MessageBoxButtons.YesNoCancel);
                switch (result)
                {
                    case DialogResult.Yes:
                        buttonSave_Click(sender, e);
                        break;
                    case DialogResult.No:
                        _workingItemConfiguration.Load();
                        Settings.Reload();
                        break;
                    default:
                        e.Cancel = true;
                        break;
                }
            }
        }

        private void linkLabelAbout_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start(linkLabelAbout.Text);
        }

        /// <summary>
        /// Handles the Selecting event of the tabControlConfiguration control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.Forms.TabControlCancelEventArgs"/> instance containing the event data.</param>
        private void tabControlConfiguration_Selecting(object sender, TabControlCancelEventArgs e)
        {
            if (e.TabPage == tabPageMappings && string.IsNullOrEmpty(Settings.Default.ConfigurationsPath))
            {
                MessageBox.Show(Resources.MappingPath, Resources.MappingPathTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                tabControlConfiguration.SelectedTab = tabPageOptions;
            }
        }
    }
}
