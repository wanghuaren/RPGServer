using System.ComponentModel.DataAnnotations;

namespace Ddxy.Common.Model.Api
{
    public class ReportErrorReq
    {
        public uint Uid { get; set; }

        public uint Rid { get; set; }

        [Required] [StringLength(3000)] public string Error { get; set; }
    }

    public class SignInReq
    {
        [Required]
        [StringLength(18, MinimumLength = 5)]
        public string UserName { get; set; }

        [Required]
        [StringLength(18, MinimumLength = 6)]
        public string Password { get; set; }

        // [Required]
        // [StringLength(18, MinimumLength = 0)]
        public string Version { get; set; }
    }

    public class SignUpReq : SignInReq
    {
        [Required]
        [StringLength(8, MinimumLength = 4)]
        public string InviteCode { get; set; }

        public bool IsRobot { get; set; }
    }

    public class CreateRoleReq
    {
        [Required] public uint? ServerId { get; set; }
        [Required] public uint? CfgId { get; set; }

        [Required]
        [StringLength(12, MinimumLength = 2)]
        public string Nickname { get; set; }
    }
}