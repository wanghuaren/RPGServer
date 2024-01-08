using System;
using System.Collections.Generic;
using Ddxy.Common.Utils;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Util;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Mail
{
    public class Mail : IDisposable
    {
        public MailEntity Entity { get; private set; }

        public List<ItemData> Items { get; private set; }

        public uint Id => Entity.Id;

        public bool Expire => TimeUtil.TimeStamp >= Entity.ExpireTime;

        public MailType Type => (MailType) Entity.Type;

        public Mail(MailEntity entity)
        {
            Entity = entity;
            Items = new List<ItemData>();
            if (!string.IsNullOrWhiteSpace(Entity.Items))
            {
                var dic = Json.SafeDeserialize<Dictionary<uint, int>>(Entity.Items);
                foreach (var (k, v) in dic)
                {
                    Items.Add(new ItemData
                    {
                        Id = k,
                        Num = (uint) v
                    });
                }
            }
        }

        public void Dispose()
        {
            Entity = null;
            Items.Clear();
            Items = null;
        }

        public MailData BuildPbData()
        {
            var pbData = new MailData
            {
                Id = Entity.Id,
                Type = (MailType) Entity.Type,
                Text = Entity.Text,
                Sender = Entity.Sender,
                CreateTime = Entity.CreateTime,
                ExpireTime = Entity.ExpireTime,
                Items = {Items},
                MinRelive = Entity.MinRelive,
                MinLevel = Entity.MinLevel,
                MaxRelive = Entity.MaxRelive,
                MaxLevel = Entity.MaxLevel,
                Picked = Entity.PickedTime > 0
            };

            return pbData;
        }
    }
}