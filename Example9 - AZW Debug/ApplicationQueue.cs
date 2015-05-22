﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Queue;

namespace Example
{
    public abstract class ApplicationQueue
    {
        public static ApplicationQueue Create(string name)
        {
            if (HttpContext.Current.Request["SERVER_NAME"] == "localhost")
                return new HttpApplicationQueue(name);

            return new AzureApplicationQueue(name);
        }

        public abstract void Append(string text);
        public abstract string ReadAll();
    }

    public class HttpApplicationQueue : ApplicationQueue
    {
        public string Name { get; private set; }

        public HttpApplicationQueue(string name)
        {
            Name = "HttpApplicationQueue/" + name;
        }

        public override void Append(string text)
        {
            HttpApplicationState appstate = HttpContext.Current.Application;
            appstate.Lock();
            appstate[Name] = (string)appstate[Name] + text;
            appstate.UnLock();
        }

        public override string ReadAll()
        {
            HttpApplicationState appstate = HttpContext.Current.Application;
            appstate.Lock();
            string text = (string)appstate[Name];
            appstate[Name] = null;
            appstate.UnLock();
            return text;
        }
    }

    public class AzureApplicationQueue : ApplicationQueue
    {
        private CloudQueue queue_;

        public AzureApplicationQueue(string name)
        {
            // Create a storage account (development storage), fails on free Azure web server
            CloudStorageAccount storageAccount = CloudStorageAccount.DevelopmentStorageAccount;
            CloudQueueClient queueClient = storageAccount.CreateCloudQueueClient();
            queue_ = queueClient.GetQueueReference(name.ToLowerInvariant());
            queue_.CreateIfNotExists();
        }

        public override void Append(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                // breaks if text is longer than 8K, article below describes how to deal with large messages
                // http://www.developerfusion.com/article/120619/advanced-scenarios-with-windows-azure-queues/
                CloudQueueMessage message = new CloudQueueMessage(text);
                queue_.AddMessage(message);
            }
        }

        public override string ReadAll()
        {
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                CloudQueueMessage message = queue_.GetMessage();
                if (message == null)
                    break;

                sb.Append(message.AsString);
            }

            return sb.ToString();
        }
    }
}