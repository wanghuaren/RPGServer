USE ddxy;

DROP TABLE IF EXISTS `pet_ornament`;
CREATE TABLE `pet_ornament`
(
    `id`         INT UNSIGNED     NOT NULL AUTO_INCREMENT
        COMMENT 'id',
    `rid`        INT UNSIGNED     NOT NULL
        COMMENT '所属角色id',
    `locked`      boolean NOT NULL DEFAULT 0
        COMMENT '锁定？',
    `typeId`     TINYINT UNSIGNED  NOT NULL
        COMMENT '类型id',
    `grade`      TINYINT UNSIGNED NOT NULL DEFAULT 0
        COMMENT '品阶',
    `place`      INT UNSIGNED NOT NULL DEFAULT 0
        COMMENT '位置，等于0-未装备, 大于0-装备了，则为宠物ID',
    `baseAttrs`  VARCHAR(2000)    NOT NULL DEFAULT ''
        COMMENT '基础属性',
    `recast`     VARCHAR(2000)    NOT NULL DEFAULT ''
        COMMENT '重铸预览数据',
    `createTime` INT UNSIGNED     NOT NULL
        COMMENT '创建时间',
    PRIMARY KEY (`id`),
    INDEX `idx_rid` (`rid`)
)
    ENGINE = InnoDB
    DEFAULT CHARSET = utf8mb4
    COMMENT ='宠物配饰表';

ALTER TABLE pet ADD `jxLevel` INT UNSIGNED NOT NULL DEFAULT 0 COMMENT '觉醒等级';
ALTER TABLE pet ADD `jxSkill` INT UNSIGNED NOT NULL DEFAULT 0 COMMENT '觉醒技能'; 