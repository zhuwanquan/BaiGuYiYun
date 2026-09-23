# 路线剧本与美术维护入口

每个直属子目录代表一条路线，`route.json` 是作者维护的主文件。菜单从已发布的路线自动生成；新增素材采用文件命名入库流程。

完整说明见[多选项与美术接入指南](../../../../Docs/多选项与美术接入指南.md)，命名细节见[美术资源命名与自动入库规则](../../../../Docs/美术资源命名与自动入库规则.md)。

## 当前保留的内容

| 路线 | 状态 | 内容 |
| --- | --- | --- |
| `canonical` | `published` | 原著线，入口 `ch27_s04_001`，复用主线后续正文 |
| `perfect` | `draft` | 完美线预留位置，待补正式剧情 |
| `fun_01`—`fun_12` | `draft` | 十二条娱乐路线预留位置，待补剧本及对应美术 |

十二个草稿没有伪造可玩的故事，正式菜单不会显示空路线。每条娱乐草稿要求一张新身份立绘，例如 `fun_01__portrait__wukong_identity__neutral`；旧兼容目录的 `entries` 已清空。

## 新增路线：先 JSON，再按名放图

1. 停止 Play，在 Unity Project 窗口复制草稿目录或创建 `fun_13`。将目录名与 `routeId` 保持一致，使用[完整 JSON 模板](../../../../ResourcePreparation/Templates/route.example.json)填写内容。
2. 娱乐线填写 `kind: "entertainment"`、`choiceGroupId: "first_encounter"`；填写 `title`、`choiceText`、`order`，保留 `status: "draft"` 和 `artReviewed: false`。
3. 填写 `entryNode` 与 `nodes`，每个节点 ID 全局唯一；补齐所有跳转与通关结尾。普通 TXT 剧本需要先整理成这个结构，不会直接自动解析成选项。
4. 按命名规则把美术和音频放到自动资源库，返回 Unity 完成导入。复制模板时同步修改路线、节点和资源 ID 中的前缀。
5. 在 `BaiguVN → Resources → Query Resource Library` 查询本路线素材，把精确资源 ID 写进剧情字段、动作及 `requiredAssets`。
6. 剧本与必需资源全部准备好并检查美术后，设 `artReviewed: true`、`status: "published"`，运行 `BaiguVN → Routes → Rebuild Published Catalog`。
7. 从正常游戏入口验收新增选项、画面、存读档与通关，再将源文件、`.meta` 和生成输出一起做本地 Git 提交。

不要为凑选项数量发布未完成草稿。按钮数量随已发布路线增减，没有写死为十二个。

## 文件名就是资源 ID

```text
Assets/_Game/ResourceLibrary/<scope>/<kind>/<scope>__<kind>__<subject>__<variant>.<ext>
```

例如 `fun_01` 的新身份立绘：

```text
Assets/_Game/ResourceLibrary/fun_01/portrait/fun_01__portrait__wukong_identity__neutral.png
```

它的 ID 为 `fun_01__portrait__wukong_identity__neutral`。`portraitId`、`transform.assetId`、`requiredAssets` 均使用这个 ID，不带目录或扩展名。

- `scope` 为 `shared` 或路线 ID；目录与文件名中的 scope、kind 必须一致，不能再嵌套子目录。
- `subject`、`variant` 只用小写英文字母、数字和单下划线；四段间用双下划线。
- `portrait`、`prop`、`effect` 使用 PNG；`background`、`cg` 支持 PNG／JPG／JPEG；`bgm`、`se`、`ambience` 使用 WAV／OGG。
- 命名素材自动登记。新路线的 `resource-catalog.asset` 可完全省略；已经存在的旧手工登记文件继续参与兼容合并，但不能与自动库重复 ID。
- 共享旧图仍由 `Assets/Resources/Routes/shared.asset` 兼容维护，不要求改名。新公共素材放自动库的 `shared` scope；生成器合并成运行时使用的 `shared-library.asset`。

查询窗口的“导出索引 JSON”会保存本地元数据到 `Library/BaiguVN/resource-library-index.json`。运行时按 ID 和 kind 精确查询已生成字典，不运行 SQL 服务，也不把索引上传云端。

## 剧情与演出边界

首次出手前的 `first_encounter` 收集整条路线。后续三次打妖怪、三次解释的位置，可在路线内增加普通 `choice` 节点：不填 `choiceGroupId`，在 `choices` 中填写各项 `id`、`label`、`next` 并补齐正文。

内部选项始终继承本路线身份，不能跳进另一条路线的私有节点或返回首次路线菜单。娱乐线可正常通关与保存，但不参加正式章节、跨轮已读、纪念、收藏、多结局或成就记录；复用共同结尾也不改变这一点。

普通画面使用 `portraitId`、`backgroundId`、`cgId`；变化过程与道具使用 `actions`。动作字段参考完整模板和 `Scripts/Runtime/Data/RouteData.cs`。`artReviewed` 是人工确认，不会自动补图或判断图画得是否正确。

## 下架、修改与存档

- 将 `status` 改为 `draft`／`disabled`，或移除该路线的 `route.json`，重建后自动减少选项。源素材不会随下架被删除。
- 共享资源或已发布路线出现命名、重复、缺图或跳转错误时，整次发布停止并保留旧输出，同时阻止使用过期目录进入 Play 或构建。
- 改菜单文字与顺序时保留 `routeId`；同一表情重绘时保留文件名、资源 ID 和 `.meta`。真正不同的剧情节点或变体使用新 ID。
- `contentRevision` 记录剧情修订，不是单独增加数字就强制回档。路线、节点或资源失效时，旧存档会提示并尝试恢复首次选择前快照；没有可用检查点则回到开场，读取不覆盖原存档。
- 画面演出稳定后再存档；恢复的是最终立绘、背景、CG、道具与音乐状态，不重放短暂特效。
- 更新以停止 Play、编辑重建、重新进入为界；玩家版本需要重新构建分发，没有线上即时热更新。

`Assets/Resources/Routes/index.json`、`<routeId>.asset`、`shared-library.asset`、`generated-files.json` 均为生成输出，不手改。`shared.asset` 是作者维护的旧共享兼容文件，生成器不覆盖或删除。

接入后的检查方法见完整指南：重点验证选项增减、长列表、正确出图、缺图阻止发布、存档恢复及娱乐线记录隔离。本说明不宣称本轮新集成测试已经全部通过。
