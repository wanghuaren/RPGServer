using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Pet
{
    public class PetSkillEntity
    {
        public uint Id { get; set; }
        public byte Lock { get; set; }
        public byte UnLock { get; set; }

        public PetSkillEntity()
        {
        }

        public PetSkillEntity(PetSkillData data)
        {
            Id = (uint) data.Id;
            Lock = (byte) (data.Lock ? 1 : 0);
            UnLock = (byte) (data.Unlock ? 1 : 0);
        }

        public PetSkillData ToData()
        {
            return new PetSkillData
            {
                Id = (SkillId) Id,
                Lock = Lock != 0,
                Unlock = UnLock != 0
            };
        }
    }
}