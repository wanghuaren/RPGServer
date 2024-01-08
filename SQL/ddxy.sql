/*
 Navicat Premium Data Transfer

 Source Server         : ddxy
 Source Server Type    : MySQL
 Source Server Version : 50738
 Source Host           : 127.0.0.1:3306
 Source Schema         : ddxy

 Target Server Type    : MySQL
 Target Server Version : 50738
 File Encoding         : 65001

 Date: 12/08/2022 09:29:43
*/


SET FOREIGN_KEY_CHECKS = 0;

DROP DATABASE IF EXISTS ddxy;
CREATE DATABASE IF NOT EXISTS ddxy DEFAULT CHARSET utf8mb4 COLLATE utf8mb4_unicode_ci;

USE ddxy;
SET NAMES utf8mb4;

-- ----------------------------
-- Table structure for admin
-- ----------------------------
DROP TABLE IF EXISTS `admin`;
CREATE TABLE `admin`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `username` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '用户名',
  `password` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '用户密码',
  `passSalt` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '密码盐',
  `nickname` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '用户昵称',
  `status` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '账号状态, 0-正常, 1-冻结',
  `category` tinyint(3) UNSIGNED NOT NULL DEFAULT 2 COMMENT '账号类型, 1-超级管理员, 2-管理员, 3-代理',
  `money` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '余额',
  `totalPay` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '总充值额度',
  `invitCode` varchar(12) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '邀请码',
  `parentId` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '所属代理id, 0表示管理员',
  `agency` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '代理等级, 0表示管理员',
  `loginIp` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '上次登录IP',
  `loginTime` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '上次登录时间',
  `createTime` int(10) UNSIGNED NOT NULL COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `idx_username`(`username`) USING BTREE,
  UNIQUE INDEX `idx_nickname`(`nickname`) USING BTREE,
  UNIQUE INDEX `idx_invit_code`(`invitCode`) USING BTREE,
  INDEX `idx_category`(`category`) USING BTREE,
  INDEX `idx_parentId`(`parentId`) USING BTREE,
  INDEX `idx_agency`(`agency`) USING BTREE,
  INDEX `idx_createTime`(`createTime`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '管理后台账号表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of admin -- ddxy1 dhxy123456
-- ----------------------------
INSERT INTO `admin` VALUES (1, 'dhxy1', 'ceMJbPXoMmwUSmq7TTQVSEZnP6M4W6IQ6xlDNUkKByg=', 'MTwnk2V6BYGMyB6L1U5v6Q==', '系统管理员', 0, 1, 0, 0, '123456', 0, 0, '115.60.85.222', 1660197116, 1606028199);

-- ----------------------------
-- Table structure for chat_msg
-- ----------------------------
DROP TABLE IF EXISTS `chat_msg`;
CREATE TABLE `chat_msg`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `fromRid` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '发送角色id',
  `toRid` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '接收角色id',
  `msgType` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '消息类型',
  `msg` varchar(1024) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '消息文本',
  `sendTime` int(10) UNSIGNED NOT NULL COMMENT '发送时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_frid`(`fromRid`) USING BTREE,
  INDEX `idx_trid`(`toRid`) USING BTREE,
  INDEX `idx_msgtype`(`msgType`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '聊天记录表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of chat_msg
-- ----------------------------

-- ----------------------------
-- Table structure for equip
-- ----------------------------
DROP TABLE IF EXISTS `equip`;
CREATE TABLE `equip`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `rid` int(10) UNSIGNED NOT NULL COMMENT '所属角色id',
  `category` tinyint(3) UNSIGNED NOT NULL COMMENT '装备类型',
  `cfgId` int(10) UNSIGNED NOT NULL COMMENT '配置id',
  `starCount` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '升星数',
  `starExp` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '升星经验',
  `gem` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '宝石镶嵌数量',
  `grade` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '品阶',
  `place` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '装备存放位置, 0-未获得, 1-穿戴, 2-背包, 3-仓库',
  `baseAttrs` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '基础属性',
  `refineAttrs` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '炼化属性',
  `needAttrs` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '需求属性',
  `refine` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '炼化预览数据',
  `refineList` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '炼化预览数据（多次炼化）',
  `recast` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '重铸预览数据',
  `createTime` int(10) UNSIGNED NOT NULL COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_rid`(`rid`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '装备表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of equip
-- ----------------------------

-- ----------------------------
-- Table structure for error
-- ----------------------------
DROP TABLE IF EXISTS `error`;
CREATE TABLE `error`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `uid` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '用户id',
  `rid` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '角色id',
  `error` varchar(3000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '错误详情',
  `remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '备注',
  `status` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '0-未处理,1-已处理,2-忽略,3-延迟',
  `createTime` int(10) UNSIGNED NOT NULL COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_uid`(`uid`) USING BTREE,
  INDEX `idx_rid`(`rid`) USING BTREE,
  INDEX `idx_status`(`status`) USING BTREE,
  INDEX `idx_createTime`(`createTime`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '错误信息表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of error
-- ----------------------------

-- ----------------------------
-- Table structure for mail
-- ----------------------------
DROP TABLE IF EXISTS `mail`;
CREATE TABLE `mail`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `sid` int(10) UNSIGNED NOT NULL COMMENT '区服id',
  `sender` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '发送者角色id, 0表示系统',
  `recver` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '接收者角色id, 0表示全区角色',
  `admin` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '后台账号id',
  `type` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '邮件类型',
  `text` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '邮件内容',
  `items` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '邮件携带内容',
  `minRelive` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '最低转生等级',
  `minLevel` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '最低等级',
  `maxRelive` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '最高转生等级',
  `maxLevel` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '最高等级',
  `remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '备注',
  `createTime` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '发布时间',
  `pickedTime` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '领取时间',
  `deleteTime` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '删除时间',
  `expireTime` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '过期时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_sid`(`sid`) USING BTREE,
  INDEX `idx_sender`(`sender`) USING BTREE,
  INDEX `idx_recver`(`recver`) USING BTREE,
  INDEX `idx_admin`(`admin`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '邮件' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of mail
-- ----------------------------

-- ----------------------------
-- Table structure for mall
-- ----------------------------
DROP TABLE IF EXISTS `mall`;
CREATE TABLE `mall`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `sid` int(10) UNSIGNED NOT NULL COMMENT '区服id',
  `rid` int(10) UNSIGNED NOT NULL COMMENT '卖方角色id',
  `dbId` int(10) UNSIGNED NOT NULL COMMENT '商品实例id',
  `cfgId` int(10) UNSIGNED NOT NULL COMMENT '商品配置id',
  `num` int(10) UNSIGNED NOT NULL COMMENT '剩余数量',
  `sellNum` int(10) UNSIGNED NOT NULL COMMENT '已出售的数量',
  `price` int(10) UNSIGNED NOT NULL COMMENT '物品单价',
  `type` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '物品类型,参考MallItemType',
  `kind` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '物品分类,参考MallItemKind',
  `detail` varbinary(1000) NULL DEFAULT NULL COMMENT '物品详情,主要装备和宠物详情',
  `createTime` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '上架时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_sid`(`sid`) USING BTREE,
  INDEX `idx_rid`(`rid`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '摆摊商品' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of mall
-- ----------------------------

-- ----------------------------
-- Table structure for mount
-- ----------------------------
DROP TABLE IF EXISTS `mount`;
CREATE TABLE `mount`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `rid` int(10) UNSIGNED NOT NULL COMMENT '所属角色id',
  `cfgId` int(10) UNSIGNED NOT NULL COMMENT '配置id',
  `name` varchar(12) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '坐骑名称',
  `level` tinyint(3) UNSIGNED NOT NULL DEFAULT 1 COMMENT '等级',
  `exp` bigint(20) UNSIGNED NOT NULL DEFAULT 0 COMMENT '经验',
  `hp` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '体力',
  `spd` int(11) NOT NULL DEFAULT 0 COMMENT '基础速度',
  `rate` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '成长率',
  `skills` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '技能id和熟练度',
  `pets` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '已管制的宠物id集合',
  `washData` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '未替换的洗练数据',
  `active` bit(1) NOT NULL DEFAULT b'0' COMMENT '是否乘骑',
  `locked` bit(1) NOT NULL DEFAULT b'0' COMMENT '是否锁定',
  `createTime` int(10) UNSIGNED NOT NULL COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_rid`(`rid`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '坐骑表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of mount
-- ----------------------------

-- ----------------------------
-- Table structure for ornament
-- ----------------------------
DROP TABLE IF EXISTS `ornament`;
CREATE TABLE `ornament`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `rid` int(10) UNSIGNED NOT NULL COMMENT '所属角色id',
  `cfgId` int(10) UNSIGNED NOT NULL COMMENT '配置id',
  `grade` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '品阶',
  `place` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '装备存放位置, 0-未获得, 1-穿戴, 2-背包, 3-仓库',
  `baseAttrs` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '基础属性',
  `recast` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '重铸预览数据',
  `createTime` int(10) UNSIGNED NOT NULL COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_rid`(`rid`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '配饰表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of ornament
-- ----------------------------

-- ----------------------------
-- Table structure for partner
-- ----------------------------
DROP TABLE IF EXISTS `partner`;
CREATE TABLE `partner`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `rid` int(10) UNSIGNED NOT NULL COMMENT '所属者角色id',
  `cfgId` int(10) UNSIGNED NOT NULL COMMENT '配置id',
  `relive` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '转生等级',
  `level` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '等级',
  `exp` bigint(20) UNSIGNED NOT NULL DEFAULT 0 COMMENT '经验值',
  `pos` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '是否参战',
  `createTime` int(10) UNSIGNED NOT NULL COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_rid`(`rid`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '角色伙伴' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of partner
-- ----------------------------

-- ----------------------------
-- Table structure for pay
-- ----------------------------
DROP TABLE IF EXISTS `pay`;
CREATE TABLE `pay`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `rid` int(10) UNSIGNED NOT NULL COMMENT '角色id',
  `money` int(10) UNSIGNED NOT NULL COMMENT '下单金额,单位元',
  `jade` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '支付后可以获得的仙玉数量',
  `bindJade` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '支付后可以获得的积分数量',
  `payChannel` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '支付渠道',
  `payType` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '支付方式',
  `remark` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '订单备注',
  `order` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '支付渠道订单编号',
  `status` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '0-已创建,1-支付成功, 2-支付失败',
  `createTime` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '创建订单时间',
  `updateTime` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '支付结果时间',
  `delivTime` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '发货时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_rid`(`rid`) USING BTREE,
  INDEX `idx_payChannel`(`payChannel`) USING BTREE,
  INDEX `payType`(`payType`) USING BTREE,
  INDEX `order`(`order`) USING BTREE,
  INDEX `idx_status`(`status`) USING BTREE,
  INDEX `idx_createTime`(`createTime`) USING BTREE,
  INDEX `idx_updateTime`(`updateTime`) USING BTREE,
  INDEX `idx_delivTime`(`delivTime`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '线上支付表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of pay
-- ----------------------------

-- ----------------------------
-- Table structure for pet
-- ----------------------------
DROP TABLE IF EXISTS `pet`;
CREATE TABLE `pet`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `rid` int(10) UNSIGNED NOT NULL COMMENT '所属角色id',
  `cfgId` int(10) UNSIGNED NOT NULL COMMENT '配置id',
  `name` varchar(12) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '宠物名称',
  `relive` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '转生等级',
  `level` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '等级',
  `exp` bigint(20) UNSIGNED NOT NULL DEFAULT 0 COMMENT '经验',
  `intimacy` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '亲密度',
  `hp` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '基础气血',
  `mp` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '基础法力',
  `atk` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '基础攻击',
  `spd` int(11) NOT NULL DEFAULT 0 COMMENT '基础速度',
  `rate` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '成长率',
  `quality` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '洗练品级',
  `keel` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '龙骨',
  `unlock` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '使用了多少聚魂丹',
  `skills` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '学习到的技能id',
  `ssSkill` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '神兽技能',
  `apAttrs` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '加点值',
  `elements` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '五行元素',
  `refineLevel` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '修炼等级',
  `refineExp` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '修炼经验',
  `refineAttrs` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '修炼属性',
  `fly` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '%10表示飞升次数, /10表示飞升增加的属性 1hp 2mp 3atk 4spd',
  `color` int(11) NOT NULL DEFAULT 0 COMMENT '变色(-1:变色未成功，0:未变色, >0变色结果)',
  `sxOrder` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '闪现支援顺序',
  `autoSkill` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '自动技能',
  `autoSyncSkill` bit(1) NOT NULL DEFAULT b'0' COMMENT '是否自动同步',
  `washData` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '未替换的洗练数据',
  `active` bit(1) NOT NULL DEFAULT b'0' COMMENT '是否参战',
  `createTime` int(10) UNSIGNED NOT NULL COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_rid`(`rid`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '宠物表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of pet
-- ----------------------------

-- ----------------------------
-- Table structure for recharge
-- ----------------------------
DROP TABLE IF EXISTS `recharge`;
CREATE TABLE `recharge`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `operator` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '操作的管理员id',
  `from` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '源id,如果是管理员则为0',
  `to` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '目标代理id',
  `money` int(11) NOT NULL DEFAULT 0 COMMENT '充值额度',
  `remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '备注',
  `createTime` int(10) UNSIGNED NOT NULL COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_from`(`from`) USING BTREE,
  INDEX `idx_to`(`to`) USING BTREE,
  INDEX `idx_createTime`(`createTime`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '代理充值表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of recharge
-- ----------------------------

-- ----------------------------
-- Table structure for recharge_role
-- ----------------------------
DROP TABLE IF EXISTS `recharge_role`;
CREATE TABLE `recharge_role`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `roleId` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '角色id',
  `parentId` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '角色所属代理id',
  `opId` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '操作员id',
  `opName` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '操作员昵称',
  `opInvitCode` varchar(12) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '操作员邀请码',
  `money` int(11) NOT NULL DEFAULT 0 COMMENT '充值额度',
  `remark` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '备注',
  `createTime` int(10) UNSIGNED NOT NULL COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_opId`(`opId`) USING BTREE,
  INDEX `idx_roleId`(`roleId`) USING BTREE,
  INDEX `idx_parentId`(`parentId`) USING BTREE,
  INDEX `idx_createTime`(`createTime`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '角色线下充值表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of recharge_role
-- ----------------------------

-- ----------------------------
-- Table structure for red_recive_record
-- ----------------------------
DROP TABLE IF EXISTS `red_recive_record`;
CREATE TABLE `red_recive_record`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `sid` int(10) UNSIGNED NOT NULL COMMENT '区服id',
  `reciveId` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '接收角色ID',
  `sendId` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '发送角色ID',
  `redId` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '红包ID',
  `redType` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '红包类型',
  `jade` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '仙玉',
  `reciveTime` int(10) UNSIGNED NOT NULL COMMENT '接收时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_sid`(`sid`) USING BTREE,
  INDEX `idx_reciveId`(`reciveId`) USING BTREE,
  INDEX `idx_sendId`(`sendId`) USING BTREE,
  INDEX `idx_redId`(`redId`) USING BTREE,
  INDEX `idx_redType`(`redType`) USING BTREE,
  INDEX `idx_reciveTime`(`reciveTime`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '红包接收记录表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of red_recive_record
-- ----------------------------

-- ----------------------------
-- Table structure for red_send_record
-- ----------------------------
DROP TABLE IF EXISTS `red_send_record`;
CREATE TABLE `red_send_record`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `sid` int(10) UNSIGNED NOT NULL COMMENT '区服id',
  `roleId` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '发送角色ID',
  `redType` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '红包类型',
  `sectId` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '帮派ID（帮派红包有效）',
  `jade` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '仙玉',
  `total` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '总计个数',
  `wish` varchar(512) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '祝福',
  `left` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '剩余个数',
  `reciver` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL COMMENT '接收者列表',
  `sendTime` int(10) UNSIGNED NOT NULL COMMENT '发送时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_roleId`(`roleId`) USING BTREE,
  INDEX `idx_redType`(`redType`) USING BTREE,
  INDEX `idx_sectId`(`sectId`) USING BTREE,
  INDEX `idx_left`(`left`) USING BTREE,
  INDEX `idx_sendTime`(`sendTime`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '红包发送记录表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of red_send_record
-- ----------------------------

-- ----------------------------
-- Table structure for role
-- ----------------------------
DROP TABLE IF EXISTS `role`;
CREATE TABLE `role`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `uid` int(10) UNSIGNED NOT NULL COMMENT '用户id',
  `sid` int(10) UNSIGNED NOT NULL COMMENT '区服id',
  `status` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '角色状态',
  `type` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '角色类型, 0-正常, 1-GM, 2-Robot',
  `nickname` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '昵称',
  `cfgId` int(10) UNSIGNED NOT NULL COMMENT '配置id',
  `sex` tinyint(3) UNSIGNED NOT NULL DEFAULT 1 COMMENT '性别, 1-男，2-女',
  `race` tinyint(3) UNSIGNED NOT NULL DEFAULT 1 COMMENT '种族, 1-人，2-仙，3-魔，4-鬼',
  `relive` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '转生等级',
  `level` tinyint(3) UNSIGNED NOT NULL DEFAULT 1 COMMENT '等级',
  `exp` bigint(20) UNSIGNED NOT NULL DEFAULT 0 COMMENT '经验值',
  `silver` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '银元值',
  `jade` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '仙玉值',
  `bindJade` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '绑定仙玉',
  `contrib` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '贡献值',
  `sldhGongJi` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '水路大会功绩',
  `guoShi` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '郭氏积分',
  `skins` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '{\"has\":[],\"use\":[]}' COMMENT '拥有的皮肤（暂时只含足迹和特效）',
  `operate` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '{\"0\":0}' COMMENT '玩家各种操作次数（1->孩子炼化 2->星阵炼化 3->配饰炼化）',
  `bianshen` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '{\"cards\":{},\"wuxing\":{},\"current\":{\"id\":0,\"timestamp\":\"0\"}}' COMMENT '变身卡及五行修炼',
  `xingzhen` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '{\"unlocked\":{},\"used\":0}' COMMENT '星阵',
  `child` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '孩子信息',
  `expExchangeTimes` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '经验兑换属性点次数',
  `mapId` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '当前所在地图id',
  `mapX` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '地图坐标X',
  `mapY` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '地图坐标Y',
  `skills` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '6个技能的熟练度用,分割',
  `color1` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '染色1',
  `color2` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '染色2',
  `sectId` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '当前加入的帮派id',
  `sectContrib` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '当前帮派贡献值',
  `sectJob` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '在帮派中的职位',
  `sectJoinTime` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '入帮时间',
  `xlLevel` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '修炼等级',
  `star` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '击杀地煞星 星级',
  `shane` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '监禁的结束时间',
  `relives` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '转生信息race_sex',
  `rewards` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '等级奖励',
  `sldh` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '水陆大会',
  `singlePk` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '单人PK',
  `flags` int(11) NOT NULL DEFAULT 0 COMMENT '各种开关',
  `autoSkill` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '自动技能',
  `autoSyncSkill` bit(1) NOT NULL DEFAULT b'1' COMMENT '是否自动同步',
  `totalPay` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '累计充值额',
  `totalPayRewards` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '累计充值领取金额集合',
  `totalPayBS` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '累计充值额',
  `dailyPay` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '今日充值额',
  `dailyPayTime` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '今日充值额记录的日期',
  `dailyPayRewards` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '今日充值领取金额集合',
  `safeCode` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '安全锁',
  `safeLocked` bit(1) NOT NULL DEFAULT b'0' COMMENT '当前是否已上锁',
  `spread` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '绑定的推广人',
  `spreadTime` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '绑定时间',
  `parentId` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '所属代理id, 0表示平台用户',
  `online` bit(1) NOT NULL DEFAULT b'0' COMMENT '当前是否在线',
  `onlineTime` int(10) UNSIGNED NOT NULL COMMENT '上次上线时间',
  `createTime` int(10) UNSIGNED NOT NULL COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uk_nickname`(`nickname`) USING BTREE,
  INDEX `idx_uid`(`uid`) USING BTREE,
  INDEX `idx_sid`(`sid`) USING BTREE,
  INDEX `idx_sectId`(`sectId`) USING BTREE,
  INDEX `idx_spread`(`spread`) USING BTREE,
  INDEX `idx_parentId`(`parentId`) USING BTREE,
  INDEX `idx_createTime`(`createTime`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 100001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '角色表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of role
-- ----------------------------

-- ----------------------------
-- Table structure for role_ext
-- ----------------------------
DROP TABLE IF EXISTS `role_ext`;
CREATE TABLE `role_ext`  (
  `rid` int(10) UNSIGNED NOT NULL COMMENT 'rid',
  `items` varchar(5000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '背包中item和数量',
  `repos` varchar(5000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '仓库中item和数量',
  `mails` varchar(1000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '我处理过的全服邮件id及其操作',
  `tiance` longtext CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL COMMENT '天策符数据',
  `qiegeLevel` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '切割等级',
  `qiegeExp` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '切割经验',
  PRIMARY KEY (`rid`) USING BTREE
) ENGINE = InnoDB CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '角色扩展表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of role_ext
-- ----------------------------

-- ----------------------------
-- Table structure for scheme
-- ----------------------------
DROP TABLE IF EXISTS `scheme`;
CREATE TABLE `scheme`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `rid` int(10) UNSIGNED NOT NULL COMMENT '所属者角色id',
  `name` varchar(8) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NULL DEFAULT NULL COMMENT '方案名字',
  `equips` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '装备',
  `ornaments` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '配饰',
  `apAttrs` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '加点属性',
  `xlAttrs` varchar(2000) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '修炼属性',
  `relives` varchar(30) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '转生信息race_sex, 使用回梦丹修改的',
  `active` bit(1) NOT NULL DEFAULT b'0' COMMENT '是否激活',
  `createTime` int(10) UNSIGNED NOT NULL COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_rid`(`rid`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '角色属性方案' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of scheme
-- ----------------------------

-- ----------------------------
-- Table structure for sect
-- ----------------------------
DROP TABLE IF EXISTS `sect`;
CREATE TABLE `sect`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `sid` int(10) UNSIGNED NOT NULL COMMENT '区服id',
  `name` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '帮派名字',
  `desc` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '帮派宗旨',
  `ownerId` int(10) UNSIGNED NOT NULL COMMENT '帮主角色id',
  `memberNum` int(10) UNSIGNED NOT NULL DEFAULT 1 COMMENT '人数',
  `contrib` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '帮派总贡献',
  `createTime` int(10) UNSIGNED NOT NULL COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uk_name`(`name`) USING BTREE,
  INDEX `idx_sid`(`sid`) USING BTREE,
  INDEX `idx_rid`(`ownerId`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '帮派' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of sect
-- ----------------------------

-- ----------------------------
-- Table structure for sectWar
-- ----------------------------
DROP TABLE IF EXISTS `sectWar`;
CREATE TABLE `sectWar`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `sid` int(10) UNSIGNED NOT NULL COMMENT '区服id',
  `season` int(10) UNSIGNED NOT NULL DEFAULT 1 COMMENT '当前第几季',
  `turn` int(10) UNSIGNED NOT NULL DEFAULT 1 COMMENT '当前第几轮',
  `lastTime` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '上次开始的时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_sid`(`sid`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '帮战' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of sectWar
-- ----------------------------

-- ----------------------------
-- Table structure for server
-- ----------------------------
DROP TABLE IF EXISTS `server`;
CREATE TABLE `server`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `name` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '区服名称',
  `status` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '区服状态, 0-正常,1-临时维护,2-永久停服',
  `recom` bit(1) NOT NULL DEFAULT b'0' COMMENT '是否为推荐',
  `rank` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '排序值, 越小越靠前',
  `addr` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '网关地址',
  `createTime` int(10) UNSIGNED NOT NULL COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uk_name`(`name`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '区服表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of server
-- ----------------------------
INSERT INTO `server` VALUES (1000, 's1', 0, b'1', 0, 'ws://127.0.0.1:20000/ws', 1660755600);

-- ----------------------------
-- Table structure for singlePk
-- ----------------------------
DROP TABLE IF EXISTS `singlePk`;
CREATE TABLE `singlePk`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `sid` int(10) UNSIGNED NOT NULL COMMENT '区服id',
  `season` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '当前第几季',
  `pkzs` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '上季获得PK战神称号的角色id集合',
  `lastTime` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '上次开始的时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_sid`(`sid`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '单人PK' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of singlePk
-- ----------------------------

-- ----------------------------
-- Table structure for sldh
-- ----------------------------
DROP TABLE IF EXISTS `sldh`;
CREATE TABLE `sldh`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `sid` int(10) UNSIGNED NOT NULL COMMENT '区服id',
  `season` int(10) UNSIGNED NOT NULL DEFAULT 1 COMMENT '当前第几季',
  `turn` int(10) UNSIGNED NOT NULL DEFAULT 1 COMMENT '当前第几轮',
  `lastTime` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '上次开始的时间',
  `slzs` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '上季获得水路战神称号的角色id集合',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_sid`(`sid`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '水陆大会' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of sldh
-- ----------------------------

-- ----------------------------
-- Table structure for ssjl
-- ----------------------------
DROP TABLE IF EXISTS `ssjl`;
CREATE TABLE `ssjl`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `sid` int(10) UNSIGNED NOT NULL COMMENT '区服id',
  `season` int(10) UNSIGNED NOT NULL DEFAULT 1 COMMENT '当前第几季',
  `lastTime` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '上次开始的时间',
  `reward` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '上季获得的神兽及角色ID',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_sid`(`sid`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '神兽降临' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of ssjl
-- ----------------------------

-- ----------------------------
-- Table structure for task
-- ----------------------------
DROP TABLE IF EXISTS `task`;
CREATE TABLE `task`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `rid` int(10) UNSIGNED NOT NULL COMMENT '所属角色id',
  `complets` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '已完成的剧情任务id集合',
  `states` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '已接受的任务id及当前的step',
  `dailyStart` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '已经接受的日常任务group集合',
  `dailyCnt` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '日常任务计数',
  `instanceCnt` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '副本任务计数',
  `activeScore` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '活动积分',
  `beenTake` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '今日奖励领取',
  `starNum` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '今日杀星次数',
  `monkeyNum` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '今日灵猴次数',
  `jinChanSongNum` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '今日金蟾送宝次数',
  `eagleNum` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '今日金翅大鹏次数',
  `updateTime` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '上次刷新时间',
  `createTime` int(10) UNSIGNED NOT NULL COMMENT '创建时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_rid`(`rid`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '角色-任务表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of task
-- ----------------------------

-- ----------------------------
-- Table structure for title
-- ----------------------------
DROP TABLE IF EXISTS `title`;
CREATE TABLE `title`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `rid` int(10) UNSIGNED NOT NULL COMMENT '所属角色id',
  `cfgId` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '称号模板id',
  `text` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '称号文本',
  `active` bit(1) NOT NULL DEFAULT b'0' COMMENT '是否穿戴',
  `createTime` int(10) UNSIGNED NOT NULL COMMENT '创建时间',
  `expireTime` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '过期时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_rid`(`rid`) USING BTREE,
  INDEX `idx_cfgId`(`cfgId`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '称号表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of title
-- ----------------------------

-- ----------------------------
-- Table structure for user
-- ----------------------------
DROP TABLE IF EXISTS `user`;
CREATE TABLE `user`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `username` varchar(32) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '用户名',
  `password` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '用户密码',
  `passSalt` varchar(64) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL COMMENT '密码盐',
  `status` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '账号状态, 0-正常, 1-冻结',
  `type` tinyint(3) UNSIGNED NOT NULL DEFAULT 0 COMMENT '账号类型, 0-正常, 1-GM, 2-Robot',
  `parentId` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '所属代理id, 0表示平台用户',
  `createTime` int(10) UNSIGNED NOT NULL COMMENT '创建时间',
  `lastLoginIp` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '上次登录IP',
  `lastLoginTime` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '上次登录时间',
  `lastUseRoleId` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '上次使用的角色id',
  PRIMARY KEY (`id`) USING BTREE,
  UNIQUE INDEX `uk_username`(`username`) USING BTREE,
  INDEX `idx_parentId`(`parentId`) USING BTREE,
  INDEX `idx_createTime`(`createTime`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 100001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '用户表' ROW_FORMAT = Dynamic;

-- ----------------------------
-- Records of user
-- ----------------------------
SET FOREIGN_KEY_CHECKS = 1;
