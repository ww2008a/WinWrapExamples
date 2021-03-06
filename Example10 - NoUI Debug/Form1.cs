﻿using Examples.Extensions;
using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

//------------------------------------------------------------------------------
// <copyright from='2013' to='2014' company='Polar Engineering and Consulting'>
//    Copyright (c) Polar Engineering and Consulting. All Rights Reserved.
//
//    This file contains confidential material.
//
// </copyright>
//------------------------------------------------------------------------------

namespace Example
{
    public partial class Form1 : Form
    {
        private Connections conns_ = new Connections(10000);
        private bool timedout_;
        private DateTime timelimit_;
        private static readonly string[] scripts_ =
        {
            "Good.bas",
            "ParseError.bas",
            "RuntimeError.bas",
            "Stop.bas",
            "TooLong.bas"
        };

        public Form1()
        {
            InitializeComponent();
            ListBoxScripts_Initialize();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Initialize Language with this as the IHost
            ScriptingLanguage.SetHost(this);
        }

        private void buttonRunScript_Click(object sender, EventArgs e)
        {
            TheIncident = new Incident();
            using (var basicNoUIObj = new WinWrap.Basic.BasicNoUIObj())
            {
                basicNoUIObj.Begin += basicNoUIObj_Begin;
                basicNoUIObj.DoEvents += basicNoUIObj_DoEvents;
                basicNoUIObj.ErrorAlert += basicNoUIObj_ErrorAlert;
                basicNoUIObj.Pause_ += basicNoUIObj_Pause_;
                basicNoUIObj.Synchronizing += basicNoUIObj_Synchronizing;
                basicNoUIObj.Secret = new System.Guid("00000000-0000-0000-0000-000000000000");
                basicNoUIObj.Initialize();

                basicNoUIObj.AttachToForm(this, WinWrap.Basic.ManageConstants.All);

                if (conns_.Any)
                {
                    basicNoUIObj.Synchronized = true;
                    basicNoUIObj.Wait(3);
                }

                /// Extend WinWrap Basic scripts with Examples.Extensions assembly
                /// Add "Imports Examples.Extensions" to all WinWrap Basic scripts
                /// Add "Imports Examples.Extensions.ScriptingLanguage" all WinWrap Basic scripts
                basicNoUIObj.AddScriptableObjectModel(typeof(ScriptingLanguage));

                try
                {
                    if (!basicNoUIObj.LoadModule(ScriptPath("Globals.bas")))
                        throw basicNoUIObj.Error.Exception;

                    using (var module = basicNoUIObj.ModuleInstance(ScriptPath(Script), false))
                    {
                        if (module == null)
                            throw basicNoUIObj.Error.Exception;

                        if (basicNoUIObj.Synchronized)
                            module.StepInto = true;

                        // Execute script code via an event
                        ScriptingLanguage.TheIncident.Start(this.Text);
                    }
                }
                catch (Exception ex)
                {
                    if (basicNoUIObj.Synchronized)
                    {
                        basicNoUIObj.ReportError(ex);
                        basicNoUIObj.Wait(3);
                    }

                    basicNoUIObj.ReportError(ex);

                    if (basicNoUIObj.Synchronized)
                        basicNoUIObj.Wait(1);
                }
            }

            TheIncident = null;
        }

        void basicNoUIObj_Begin(object sender, EventArgs e)
        {
            timedout_ = false;
            timelimit_ = DateTime.Now + new TimeSpan(0, 0, 1);
        }

        void basicNoUIObj_DoEvents(object sender, EventArgs e)
        {
            WinWrap.Basic.BasicNoUIObj basicNoUIObj = sender as WinWrap.Basic.BasicNoUIObj;
            if (basicNoUIObj.Synchronized)
            {
                conns_.ForEachConnection(conn =>
                {
                    string[] commands = conn.GetReceviedData("\r\n");
                    foreach (string command in commands)
                    {
                        string param = Encoding.UTF8.GetString(Convert.FromBase64String(command));
                        basicNoUIObj.Synchronize(param, conn.Id);
                    }
                });
            }
            else if (DateTime.Now >= timelimit_)
            {
                timedout_ = true;
                // time timelimit has been reached, terminate the script
                basicNoUIObj.Run = false;
            }
        }

        void basicNoUIObj_ErrorAlert(object sender, EventArgs e)
        {
            WinWrap.Basic.BasicNoUIObj basicNoUIObj = sender as WinWrap.Basic.BasicNoUIObj;
            LogError(basicNoUIObj.Error);
        }

        void basicNoUIObj_Pause_(object sender, EventArgs e)
        {
            WinWrap.Basic.BasicNoUIObj basicNoUIObj = sender as WinWrap.Basic.BasicNoUIObj;
            if (!basicNoUIObj.Synchronized)
            {
                // Script execution has paused, terminate the script
                if (basicNoUIObj.Error == null)
                {
                    LogError(Examples.SharedSource.WinWrapBasic.FormatTimeoutError(basicNoUIObj, timedout_));
                }
                // Script execution has paused, terminate the script
                basicNoUIObj.Run = false;
            }
        }

        void basicNoUIObj_Synchronizing(object sender, WinWrap.Basic.Classic.SynchronizingEventArgs e)
        {
            string response = Convert.ToBase64String(Encoding.UTF8.GetBytes(e.Param)) + "\r\n";
            conns_.ForEachConnection(conn =>
            {
                if (e.Id < 0 || e.Id == conn.Id)
                    conn.Send(response);
            });
        }

        private void listBoxScripts_SelectedIndexChanged(object sender, EventArgs e)
        {
            textBoxScript.Text = File.ReadAllText(ScriptPath(Script));
        }
    }
}
