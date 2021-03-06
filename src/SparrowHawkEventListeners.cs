﻿using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using Rhino;
using Rhino.Commands;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.DocObjects.Tables;

namespace SparrowHawk
{
    /// <summary>
    /// Used to send messages from Rhino to SparrowHawk asynchronously.
    /// </summary>
    public class SparrowHawkSignal
    {
        public enum ESparrowHawkSigalType
        {
            InitType, LineType, EncoderType, CutType
        };

        public ESparrowHawkSigalType type;
        public float[] data;
        public string strData; 
        public SparrowHawkSignal(ESparrowHawkSigalType _type, float[] _data)
        {
            type = _type; data = _data; 
        }
    }

    class SparrowHawkEventListeners
    {
        // TODO: This is dangerous, should implement P-C-Q myself. 
        #region Members
        private readonly EventHandler<RhinoObjectEventArgs> m_add_rhino_object_handler;
        private readonly EventHandler<RhinoModifyObjectAttributesEventArgs> m_modify_rhino_attributes_handler;
        //private Queue<SparrowHawkSignal> mSignalQueue;
        protected ConcurrentQueue<SparrowHawkSignal> mSignalQueue;
        #endregion

        SparrowHawkEventListeners()
        {
            m_add_rhino_object_handler = new EventHandler<RhinoObjectEventArgs>(OnAddRhinoObject);
            m_modify_rhino_attributes_handler = new EventHandler<RhinoModifyObjectAttributesEventArgs>(OnModifyObjectAttributes);
            mSignalQueue = new ConcurrentQueue<SparrowHawkSignal>();
        }

        public bool IsEnabled { get; private set; }

        /// <summary>
        /// The one and only EventWatcherHandlers object
        /// </summary>
        static SparrowHawkEventListeners g_instance;


        /// <summary>
        /// Returns the one and only EventWatcherHandlers object
        /// </summary>
        public static SparrowHawkEventListeners Instance
        {
            get { return g_instance ?? (g_instance = new SparrowHawkEventListeners()); }
        }

        #region Events
        /// <summary>
        /// Parses any rhino object that is added, and sends it safely to the 
        /// VR thread if necessary.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void OnAddRhinoObject(object sender, Rhino.DocObjects.RhinoObjectEventArgs e)
        {
            processSignal(e.TheObject.Attributes.Name);
        }

        void OnModifyObjectAttributes(object sender, Rhino.DocObjects.RhinoModifyObjectAttributesEventArgs e)
        {
            processSignal(e.NewAttributes.Name);
        }

        void processSignal(string str)
        {
            char[] delimiters = { ' ', ',' };
            if (str == "") return;
            string[] substrings = str.Split(delimiters, StringSplitOptions.RemoveEmptyEntries);
            if (substrings.Length == 0) return;

            SparrowHawkSignal s = new SparrowHawkSignal(SparrowHawkSignal.ESparrowHawkSigalType.InitType, new float[substrings.Length - 1]);
            for (int i = 1; i < substrings.Length; i++)
            {
                if (substrings[0] != "del:")
                {
                    if (!float.TryParse(substrings[i], out s.data[i - 1]))
                        return;
                }
                else
                {
                    s.strData = substrings[1];
                }
            }
            switch (substrings[0])
            {
                case "init:":
                    RhinoApp.WriteLine("Calibration point recieved");
                    s.type = SparrowHawkSignal.ESparrowHawkSigalType.InitType;
                    mSignalQueue.Enqueue(s);
                    break;
                case "angle:":
                    s.type = SparrowHawkSignal.ESparrowHawkSigalType.EncoderType;
                    mSignalQueue.Enqueue(s);
                    break;
                case "stroke:":
                    s.type = SparrowHawkSignal.ESparrowHawkSigalType.LineType;
                    mSignalQueue.Enqueue(s);
                    break;
                case "del:":
                    s.type = SparrowHawkSignal.ESparrowHawkSigalType.CutType;
                    mSignalQueue.Enqueue(s);
                    break;
            }
            //mSignalQueue.Enqueue(s);

        }

        #endregion

        /// <summary>
        /// If any Signals have been recorded, return the earliest one. Otherwise null.
        /// </summary>
        /// <returns></returns>
        public SparrowHawkSignal getOneSignal()
        {
            SparrowHawkSignal s;
            if (mSignalQueue.TryDequeue(out s))
                return s;
            return null;
        }

        public void Enable(bool enable)
        {
            if (enable != IsEnabled)
            {
                if (enable)
                {
                    RhinoDoc.AddRhinoObject += m_add_rhino_object_handler;
                    RhinoDoc.ModifyObjectAttributes += m_modify_rhino_attributes_handler;
                }
                else
                {
                    RhinoDoc.AddRhinoObject -= m_add_rhino_object_handler;
                    RhinoDoc.ModifyObjectAttributes -= m_modify_rhino_attributes_handler;
                }
            }
            IsEnabled = enable;
        }


//        public impatientConsume{}

    }
}
