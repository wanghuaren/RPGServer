using Ddxy.Protocol;

namespace Ddxy.GameServer.Data.Vo
{
    public class MountSkillVo
    {
        public uint Id { get; set; }

        public uint Exp { get; set; }

        public int Level { get; set; }

        public MountSkillVo()
        {
            
        }

        public MountSkillVo(MountSkillData pbData)
        {
            Id = pbData.CfgId;
            Exp = pbData.Exp;
            Level = pbData.Level;
        }
    }
}