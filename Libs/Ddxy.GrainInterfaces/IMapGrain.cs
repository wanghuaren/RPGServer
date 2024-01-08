using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;

namespace Ddxy.GrainInterfaces
{
    /// <summary>
    /// 表示一张地图, key为 "{serverId}_{mapId}"
    /// </summary>
    public interface IMapGrain : IGrainWithStringKey
    {
        Task StartUp();

        Task ShutDown();

        ValueTask<bool> CheckActive();

        Task Enter(Immutable<byte[]> mapObjectBytes, uint deviceWidth, uint deviceHeight);

        Task Exit(uint onlyId);

        Task PlayerMove(uint onlyId, int x, int y, bool blink);

        Task TeamMove(Immutable<byte[]> reqBytes);

        Task PlayerOnline(uint onlyId, uint deviceWidth, uint deviceHeight);

        Task PlayerOffline(uint onlyId);
        
        Task PlayerPause(uint onlyId, bool pause);

        Task PlayerEnterBattle(uint onlyId, uint battleId, uint campId);

        Task PlayerExitBattle(uint onlyId);

        Task SetPlayerName(uint onlyId, string name);

        Task SetPlayerLevel(uint onlyId, uint relive, uint level);

        Task SetPlayerCfgId(uint onlyId, uint cfgId);

        Task SetPlayerColor(uint onlyId, uint color1, uint color2);

        Task SetPlayerTeam(uint onlyId, uint teamId, uint teamLeader, uint memberCount);

        Task SetPlayerSect(uint onlyId, uint sectId);

        Task SetPlayerWeapon(uint onlyId, Immutable<byte[]> weaponDataBytes);

        Task SetPlayerWing(uint onlyId, Immutable<byte[]> wingDataBytes);

        Task SetPlayerSkins(uint onlyId, List<int> skins);

        Task SetPlayerVipLevel(uint onlyId, uint vipLevel);

        Task SetPlayerBianshen(uint onlyId, int cardId);

        Task SetPlayerQieGeLevel(uint onlyId, uint qieGeLevel);

        Task SetPlayerMount(uint onlyId, uint cfgId);

        Task SetPlayerTitle(uint onlyId, Immutable<byte[]> titleDataBytes);

        Task SetPlayerSldhGroup(uint onlyId, uint sldhGroup);

        Task SetPlayerWzzzGroup(uint onlyId, uint sldhGroup);

        Task SetPlayerTeamLeave(uint onlyId, bool leave);
    }
}