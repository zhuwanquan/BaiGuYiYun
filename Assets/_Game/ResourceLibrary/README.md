# 按名称自动入库的素材

把新素材放在 `<scope>/<kind>/<scope>__<kind>__<subject>__<variant>.<ext>`。

例如 `fun_13/portrait/fun_13__portrait__wukong_clerk__neutral.png`；剧本填写 `fun_13__portrait__wukong_clerk__neutral`，无需拖入资源目录。

`shared` 是共同素材，其他 scope 与路线 ID 一致。图片由命名导入器设置为单张 Sprite。不要在这里放绘画工程文件、多个版本或未按规则命名的成品；制作源稿可留在 Assets 外。

通过 `BaiguVN → Resources → Query Resource Library` 按范围、类型和关键字查询。完整规则见工程 `Docs/美术资源命名与自动入库规则.md`。

此处只管理源素材。发布目录由生成器重建，不会把未发布路线的美术自动装入正式路线。现有旧美术保留原路径和旧 ID，通过兼容资源目录继续使用。
