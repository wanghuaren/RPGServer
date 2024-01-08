/*
 Navicat Premium Data Transfer

 Source Server         : jxxy116.62.149.238
 Source Server Type    : MySQL
 Source Server Version : 50739
 Source Host           : localhost:3306
 Source Schema         : ddxy

 Target Server Type    : MySQL
 Target Server Version : 50739
 File Encoding         : 65001

 Date: 23/09/2022 10:04:31
*/

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- ----------------------------
-- Table structure for wzzz
-- ----------------------------
DROP TABLE IF EXISTS `wzzz`;
CREATE TABLE `wzzz`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `sid` int(10) UNSIGNED NOT NULL COMMENT '区服id',
  `season` int(10) UNSIGNED NOT NULL DEFAULT 1 COMMENT '当前第几季',
  `turn` int(10) UNSIGNED NOT NULL DEFAULT 1 COMMENT '当前第几轮',
  `lastTime` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '上次开始的时间',
  `slzs` varchar(500) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '上季获得王者之战战神称号的角色id集合',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_sid`(`sid`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '王者之战' ROW_FORMAT = DYNAMIC;

SET FOREIGN_KEY_CHECKS = 1;


ALTER TABLE role ADD `WzzzJiFen`  INT UNSIGNED NOT NULL DEFAULT 0 COMMENT '王者之战积分' AFTER `sldhGongJi`;
ALTER TABLE role ADD `wzzz` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '王者之战' AFTER `sldh`;
ALTER TABLE role ADD `ewaiPay` INT UNSIGNED NOT NULL DEFAULT 0 COMMENT '额外累计充值' AFTER `totalPayBS`;
ALTER TABLE role ADD `ewaiPayRewards` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '额外累计充值领取金额集合' AFTER `ewaiPay`;


ALTER TABLE role_ext ADD `szlHurtLv`  INT UNSIGNED NOT NULL DEFAULT 0 COMMENT '神之力-伤害等级' AFTER `qiegeExp`;
ALTER TABLE role_ext ADD `szlHpLv`  INT UNSIGNED NOT NULL DEFAULT 0 COMMENT '神之力-气血等级' AFTER `szlHurtLv`;
ALTER TABLE role_ext ADD `szlSpeedLv`  INT UNSIGNED NOT NULL DEFAULT 0 COMMENT '神之力-速度等级' AFTER `szlHpLv`;
ALTER TABLE role ADD `cszlLayer`  INT UNSIGNED NOT NULL DEFAULT 0 COMMENT '成神之路-爬塔层数' AFTER `WzzzJiFen`;


-- ----------------------------
-- Table structure for daLuanDou
-- ----------------------------
DROP TABLE IF EXISTS `daLuanDou`;
CREATE TABLE `daLuanDou`  (
  `id` int(10) UNSIGNED NOT NULL AUTO_INCREMENT COMMENT 'id',
  `sid` int(10) UNSIGNED NOT NULL COMMENT '区服id',
  `turn` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '当前第几轮',
  `season` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '当前第几季',
  `pkzs` varchar(200) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '上季获得大乱斗PK战神称号的角色id集合',
  `lastTime` int(10) UNSIGNED NOT NULL DEFAULT 0 COMMENT '上次开始的时间',
  PRIMARY KEY (`id`) USING BTREE,
  INDEX `idx_sid`(`sid`) USING BTREE
) ENGINE = InnoDB AUTO_INCREMENT = 1001 CHARACTER SET = utf8mb4 COLLATE = utf8mb4_general_ci COMMENT = '大乱斗PK' ROW_FORMAT = Dynamic;

ALTER TABLE role ADD `daLuanDou` varchar(100) CHARACTER SET utf8mb4 COLLATE utf8mb4_general_ci NOT NULL DEFAULT '' COMMENT '大乱斗' AFTER `singlePk`;
