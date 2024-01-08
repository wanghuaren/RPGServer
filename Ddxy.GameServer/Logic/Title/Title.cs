using System.Threading.Tasks;
using Ddxy.Common.Utils;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Grains;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Title
{
    public class Title
    {
        public TitleEntity Entity { get; private set; }
        private TitleEntity _lastEntity; //上一次更新的Entity

        public uint Id => Entity.Id;
        public uint CfgId => Entity.CfgId;

        public bool Active
        {
            get => Entity.Active;
            set => Entity.Active = value;
        }

        public bool Expire => Entity.ExpireTime >= TimeUtil.TimeStamp;

        public Title(PlayerGrain player, TitleEntity entity)
        {
            Entity = entity;
            _lastEntity = new TitleEntity();
            _lastEntity.CopyFrom(Entity);
        }

        public async Task Destroy()
        {
            await SaveData(false);
            _lastEntity = null;
            Entity = null;
        }

        public async Task SaveData(bool copy = true)
        {
            if (Entity.Equals(_lastEntity)) return;
            var ret = await DbService.UpdateEntity(_lastEntity, Entity);
            if (ret && copy) _lastEntity.CopyFrom(Entity);
        }

        public TitleData BuildPbData()
        {
            return new()
            {
                Id = Id,
                CfgId = CfgId,
                Text = Entity.Text,
                Active = Entity.Active,
                CreateTime = Entity.CreateTime,
                ExpireTime = Entity.ExpireTime
            };
        }
    }
}