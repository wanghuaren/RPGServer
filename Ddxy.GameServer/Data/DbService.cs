using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data.Entity;
using Ddxy.Protocol;
using FreeSql;
using FreeSql.Aop;
using Microsoft.Extensions.Logging;

namespace Ddxy.GameServer.Data
{
    public static class DbService
    {
        public static IFreeSql Sql { get; private set; }

        private static readonly ILogger Logger = XLogFactory.Create(typeof(DbService));

        public static Task Init(string connection)
        {
            Close();
            Sql = new FreeSqlBuilder()
                .UseConnectionString(DataType.MySql, connection)
                .UseAutoSyncStructure(false)
                .UseNoneCommandParameter(true)
                .UseExitAutoDisposePool(false)
                .Build();
            // 对fsql进行切片编程, 便于监控sql日志
            Sql.Aop.CurdAfter += SqlCurdAfter;
            return Task.CompletedTask;
        }

        public static Task Close()
        {
            Sql?.Dispose();
            Sql = null;
            return Task.CompletedTask;
        }

        private static void SqlCurdAfter(object sender, CurdAfterEventArgs args)
        {
            var sql = args.Sql;
            if (args.DbParms.Length > 0)
            {
                foreach (var para in args.DbParms)
                {
                    var pvalue = para.Value.ToString();
                    if (para.DbType == DbType.String)
                    {
                        pvalue = "\'" + pvalue + "\'";
                    }

                    sql = sql.Replace(para.ParameterName, pvalue);
                }
            }
            // 聊天日志只输出异常
            if (sql.IndexOf("chat_msg") != -1)
            {
                // 输出异常
                if (args.Exception != null)
                    Logger.LogError(args.Exception, $"sql: {sql} ---");
                return;
            }
            // 不要换行
            sql = sql.Replace("\r\n", " ");
            // 输出操作sql日志
            // FIXME: MySQL正常日志输出，暂时匹配
            // Logger.LogDebug($"({args.ElapsedMilliseconds}ms) {sql} ");
            // 输出异常
            if (args.Exception != null)
            {
                Logger.LogError(args.Exception, $"sql: {sql} ---");
            }
        }

        public static async Task<List<uint>> ListNormalServerId()
        {
            var rows = await Sql.Queryable<ServerEntity>()
                .Where(it => it.Status == ServerStatus.Normal)
                .ToListAsync(it => it.Id);
            return rows;
        }

        public static async Task<ServerEntity> QueryServer(uint serverId)
        {
            var res = await Sql.Queryable<ServerEntity>().Where(it => it.Id == serverId).FirstAsync();
            return res;
        }

        public static async Task<bool> HasUser(string username)
        {
            var ret = await Sql.Queryable<UserEntity>().Where(it => it.UserName == username).CountAsync();
            return ret > 0;
        }

        public static async Task<UserEntity> QueryUser(string username)
        {
            var ret = await Sql.Queryable<UserEntity>().Where(it => it.UserName == username).FirstAsync();
            return ret;
        }

        public static async Task<bool> InsertUser(UserEntity entity)
        {
            using var repo = Sql.GetRepository<UserEntity>();
            await repo.InsertAsync(entity);
            return entity.Id > 0;
        }

        public static async Task<uint> QueryLastUseRoleId(uint userId)
        {
            var rid = await Sql.Queryable<UserEntity>()
                .Where(it => it.Id == userId)
                .FirstAsync(it => it.LastUseRoleId);
            return rid;
        }

        /// <summary>
        /// 检查指定用户id在指定区服id下是否有角色
        /// </summary>
        public static async Task<bool> HasRole(uint userId, uint serverId)
        {
            var ret = await Sql.Queryable<RoleEntity>()
                .Where(it => it.UserId == userId && it.ServerId == serverId)
                .CountAsync();
            return ret > 0;
        }

        public static async Task<bool> HasRole(string nickname)
        {
            var ret = await Sql.Queryable<RoleEntity>().Where(it => it.NickName == nickname)
                .CountAsync();
            return ret > 0;
        }

        /// <summary>
        /// 创建角色, 事务
        /// </summary>
        public static async Task<bool> CreateRole(
            RoleEntity entity,
            RoleExtEntity ext,
            List<SchemeEntity> schemes,
            TaskEntity task)
        {
            // 事务方式
            using var uow = Sql.CreateUnitOfWork();
            // 创建角色
            {
                var repo = uow.GetRepository<RoleEntity>();
                await repo.InsertAsync(entity);
                if (entity.Id == 0)
                {
                    uow.Rollback();
                    return false;
                }
            }
            // 创建角色扩展信息
            {
                var repo = uow.GetRepository<RoleExtEntity>();
                ext.RoleId = entity.Id;
                var tmp = await repo.InsertAsync(ext);
                if (tmp == null)
                {
                    uow.Rollback();
                    return false;
                }
            }
            // 创建默认的Scheme
            {
                foreach (var scheme in schemes)
                {
                    var repo = uow.GetRepository<SchemeEntity>();
                    scheme.RoleId = entity.Id;
                    await repo.InsertAsync(scheme);
                    if (scheme.Id == 0)
                    {
                        uow.Rollback();
                        return false;
                    }
                }
            }
            // 创建任务配置
            {
                task.RoleId = entity.Id;
                var repo = uow.GetRepository<TaskEntity>();
                await repo.InsertAsync(task);
                if (task.Id == 0)
                {
                    uow.Rollback();
                    return false;
                }
            }

            // 提交事务
            uow.Commit();
            return true;
        }

        public static async Task<List<uint>> QueryRoles(uint userId)
        {
            var rows = await Sql.Queryable<RoleEntity>()
                .Where(it => it.UserId == userId)
                .ToListAsync(it => it.Id);
            return rows;
        }

        public static async Task<RoleEntity> QueryRole(uint roleId)
        {
            var row = await Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == roleId)
                .FirstAsync();
            return row;
        }
        
        public static async Task<RoleExtEntity> QueryRoleExt(uint roleId)
        {
            var row = await Sql.Queryable<RoleExtEntity>()
                .Where(it => it.RoleId == roleId)
                .FirstAsync();
            return row;
        }

        public static async Task<bool> UpdateRoleName(uint id, string nickname)
        {
            try
            {
                var er = await Sql.Update<RoleEntity>()
                    .Where(it => it.Id == id)
                    .Set(it => it.NickName, nickname)
                    .ExecuteAffrowsAsync();
                return er == 1;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static async Task<bool> UpdateRoleSect(uint id, uint sectId, uint sectContrib, byte sectJob,
            uint sectJoinTime)
        {
            var er = await Sql.Update<RoleEntity>()
                .Where(it => it.Id == id)
                .Set(it => it.SectId, sectId)
                .Set(it => it.SectContrib, sectContrib)
                .Set(it => it.SectJob, sectJob)
                .Set(it => it.SectJoinTime, sectJoinTime)
                .ExecuteAffrowsAsync();
            return er > 0;
        }

        // 重置单人PK排行榜
        public static async Task<bool> ResetAllRoleSinglePk(uint serverId, List<uint> excludedRoleIDs)
        {
            var er = await Sql.Update<RoleEntity>()
                .Where(it => it.ServerId == serverId && !excludedRoleIDs.Contains(it.Id))
                .Set(it => it.SinglePk, "")
                .ExecuteAffrowsAsync();
            return er > 0;
        }

        // 大乱斗PK排行榜
        public static async Task<bool> ResetAllRoleDaLuanDou(uint serverId, List<uint> excludedRoleIDs)
        {
            var er = await Sql.Update<RoleEntity>()
                .Where(it => it.ServerId == serverId && !excludedRoleIDs.Contains(it.Id))
                .Set(it => it.DaLuanDou, "")
                .ExecuteAffrowsAsync();
            return er > 0;
        }

        public static async Task<TaskEntity> QueryTask(uint roleId)
        {
            var row = await Sql.Queryable<TaskEntity>().Where(it => it.RoleId == roleId).FirstAsync();
            return row;
        }

        public static async Task<List<EquipEntity>> QueryEquips(uint roleId)
        {
            var rows = await Sql.Queryable<EquipEntity>()
                .Where(it => it.RoleId == roleId)
                .ToListAsync();
            return rows;
        }

        public static async Task<List<OrnamentEntity>> QueryOrnaments(uint roleId)
        {
            var rows = await Sql.Queryable<OrnamentEntity>()
                .Where(it => it.RoleId == roleId)
                .ToListAsync();
            return rows;
        }

        public static async Task<List<PetOrnamentEntity>> QueryPetOrnaments(uint roleId)
        {
            var rows = await Sql.Queryable<PetOrnamentEntity>()
                .Where(it => it.RoleId == roleId)
                .ToListAsync();
            return rows;
        }

        public static async Task<List<PetEntity>> QueryPets(uint roleId)
        {
            var rows = await Sql.Queryable<PetEntity>()
                .Where(it => it.RoleId == roleId)
                .ToListAsync();
            return rows;
        }

        public static async Task<List<MountEntity>> QueryMounts(uint roleId)
        {
            var rows = await Sql.Queryable<MountEntity>()
                .Where(it => it.RoleId == roleId)
                .ToListAsync();
            return rows;
        }

        public static async Task<List<PartnerEntity>> QueryPartners(uint roleId)
        {
            var rows = await Sql.Queryable<PartnerEntity>()
                .Where(it => it.RoleId == roleId)
                .ToListAsync();
            return rows;
        }

        public static async Task<SectEntity> QuerySect(uint sectId)
        {
            var row = await Sql.Queryable<SectEntity>().Where(it => it.Id == sectId).FirstAsync();
            return row;
        }

        public static async Task<List<uint>> QuerySects(uint server)
        {
            var rows = await Sql.Queryable<SectEntity>()
                .Where(it => it.ServerId == server)
                .OrderByDescending(it => it.Contrib)
                .ToListAsync(it => it.Id);
            return rows;
        }
        
        public static async Task<List<TitleEntity>> QueryTitles(uint roleId)
        {
            var rows = await Sql.Queryable<TitleEntity>()
                .Where(it => it.RoleId == roleId)
                .ToListAsync();
            return rows;
        }

        public static async Task<bool> UpdateSectOwner(uint sectId, uint roleId)
        {
            var er = await Sql.Update<SectEntity>()
                .Where(it => it.Id == sectId)
                .Set(it => it.OwnerId == roleId)
                .ExecuteAffrowsAsync();
            return er > 0;
        }

        public static async ValueTask<bool> ExistsSect(string name)
        {
            var c = await Sql.Queryable<SectEntity>().Where(it => it.Name == name).CountAsync();
            return c > 0;
        }

        public static async Task<List<SectMemberData>> QuerySectMembers(uint sectId)
        {
            var rows = await Sql.Queryable<RoleEntity>().Where(it => it.SectId == sectId).ToListAsync(
                it => new SectMemberData
                {
                    Id = it.Id,
                    Name = it.NickName,
                    Relive = it.Relive,
                    Level = it.Level,
                    CfgId = it.CfgId,
                    Contrib = it.SectContrib,
                    Type = (SectMemberType) it.SectJob,
                    JoinTime = it.SectJoinTime,
                    Skins = {},
                    Weapon = new MapObjectEquipData(),
                    Wing = new MapObjectEquipData(),
                });
            return rows;
        }

        public static async Task<List<SchemeEntity>> QuerySchemes(uint roleId)
        {
            var rows = await Sql.Queryable<SchemeEntity>().Where(it => it.RoleId == roleId).ToListAsync();
            return rows;
        }

        public static async Task<SldhEntity> QuerySldh(uint serverId)
        {
            var row = await Sql.Queryable<SldhEntity>()
                .Where(it => it.ServerId == serverId).FirstAsync();
            return row;
        }
        
        public static async Task<WzzzEntity> QueryWzzz(uint serverId)
        {
            var row = await Sql.Queryable<WzzzEntity>()
                .Where(it => it.ServerId == serverId).FirstAsync();
            return row;
        }
        
        public static async Task<SinglePkEntity> QuerySinglePk(uint serverId)
        {
            var row = await Sql.Queryable<SinglePkEntity>()
                .Where(it => it.ServerId == serverId).FirstAsync();
            return row;
        }

        public static async Task<DaLuanDouEntity> QueryDaLuanDou(uint serverId)
        {
            var row = await Sql.Queryable<DaLuanDouEntity>()
                .Where(it => it.ServerId == serverId).FirstAsync();
            return row;
        }

        public static async Task<SectWarEntity> QuerySectWar(uint serverId)
        {
            var row = await Sql.Queryable<SectWarEntity>()
                .Where(it => it.ServerId == serverId).FirstAsync();
            return row;
        }

        // 神兽降临--活动记录
        public static async Task<SsjlEntity> QuerySsjl(uint serverId)
        {
            var row = await Sql.Queryable<SsjlEntity>()
                .Where(it => it.ServerId == serverId).FirstAsync();
            return row;
        }

        public static async Task<List<MallEntity>> QueryMalls(uint serverId, int limit)
        {
            var rows = await Sql.Queryable<MallEntity>().Where(it => it.ServerId == serverId).Limit(limit)
                .ToListAsync();
            return rows;
        }

        public static async Task<List<MailEntity>> QuerySystemMails(uint serverId)
        {
            var rows = await Sql.Queryable<MailEntity>()
                .Where(it => it.ServerId == serverId && it.Recver == 0)
                .ToListAsync();
            return rows;
        }

        /// <summary>
        /// 批量插入聊天记录
        /// </summary>
        public static async Task<int> InsertChatMsgBulk(List<ChatMsgEntity> list)
        {
            return await Sql.Insert<ChatMsgEntity>(list).ExecuteAffrowsAsync();
        }

        /// <summary>
        /// 插入实体，新增数据记录
        /// </summary>
        public static async Task InsertEntity<T>(T entity) where T : class
        {
            var repo = Sql.GetRepository<T>();
            await repo.InsertAsync(entity);
        }

        /// <summary>
        /// 删除实体
        /// </summary>
        public static async Task<bool> DeleteEntity<T>(object primaryKey) where T : class
        {
            var ret = await Sql.Delete<T>(primaryKey).ExecuteAffrowsAsync();
            return ret > 0;
        }

        /// <summary>
        /// 增量式更新数据
        /// </summary>
        /// <param name="old">上次更新后的Entity</param>
        /// <param name="cur">本次待更新的Entity</param>
        /// <typeparam name="T">Entity</typeparam>
        /// <returns>更新是否成功</returns>
        public static async Task<bool> UpdateEntity<T>(T old, T cur) where T : class
        {
            using var repo = Sql.GetRepository<T>();
            repo.Attach(old);
            var er = await repo.UpdateAsync(cur);
            return er == 1;
        }
    }
}