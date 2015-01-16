﻿using IntegrationEngine.Configuration;
using IntegrationEngine.Core.Jobs;
using IntegrationEngine.Core.Mail;
using IntegrationEngine.Core.Storage;
using log4net;
using Nest;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace IntegrationEngine.MessageQueue
{
    public class RabbitMqListener
    {
        public IList<Type> IntegrationJobTypes { get; set; }
        public MessageQueueConfiguration MessageQueueConfiguration { get; set; }
        public MessageQueueConnection MessageQueueConnection { get; set; }
        public ILog Log { get; set; }
        public IMailClient MailClient { get; set; }
        public IntegrationEngineContext IntegrationEngineContext { get; set; }
        public IElasticClient ElasticClient { get; set; }

        public RabbitMqListener()
        {}

        public void Listen()
        {
            var connection = MessageQueueConnection.GetConnection();
            using (var channel = connection.CreateModel())
            {
                var consumer = new QueueingBasicConsumer(channel);
                channel.BasicConsume(MessageQueueConfiguration.QueueName, true, consumer);

                Log.Info("Waiting for messages...");
                while (true)
                {
                    var eventArgs = (BasicDeliverEventArgs)consumer.Queue.Dequeue();
                    var body = eventArgs.Body;
                    var message = Encoding.UTF8.GetString(body);
                    Log.Info(string.Format("Received {0}", message));
                    if (IntegrationJobTypes != null && !IntegrationJobTypes.Any())
                        continue;
                    var type = IntegrationJobTypes.FirstOrDefault(t => t.FullName.Equals(message));
                    var integrationJob = Activator.CreateInstance(type) as IIntegrationJob;
                    integrationJob = AutoWireJob(integrationJob, type);
                    if (integrationJob != null)
                        integrationJob.Run();
                }
            }
        }

        T AutoWireJob<T>(T job, Type type)
        {
            if (type.GetInterface(typeof(IMailJob).Name) != null)
                (job as IMailJob).MailClient = MailClient;
            if (type.GetInterface(typeof(ISqlJob).Name) != null)
                (job as ISqlJob).DbContext = IntegrationEngineContext;
            if (type.GetInterface(typeof(ILogJob).Name) != null)
                (job as ILogJob).Log = Log;
            if (type.GetInterface(typeof(IElasticsearchJob).Name) != null)
                (job as IElasticsearchJob).ElasticClient = ElasticClient;
            return job;
        }
    }
}