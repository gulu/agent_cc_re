// DbSeed — 数据库种子数据（增量模式）
// 不覆盖已有数据，只补充缺失条目
// 每次启动时执行，确保新增加的规则自动入库
// ★ 2025版：基于《放射诊断报告书写规范专家共识》、DB11/T 2505—2025 全面扩充

using FreeSql;
using Newtonsoft.Json;
using ReportQC.Entities;

namespace ReportQC.Services;

public static class DbSeed
{
    public static void Initialize(IFreeSql fsql)
    {
        SeedScoreDimensions(fsql);
        SeedKnowledgeBase(fsql);
        SeedTerminology(fsql);
        SeedRadsStandards(fsql);
    }

    private static void InsertIfNotExists(IFreeSql fsql, List<KnowledgeBase> items)
    {
        var allExisting = fsql.Select<KnowledgeBase>().ToList(k => new { k.CategoryCode, k.MatchKey });
        var existingSet = allExisting.Select(e => e.CategoryCode + "|" + e.MatchKey).ToHashSet();
        var toInsert = items.Where(i => !existingSet.Contains(i.CategoryCode + "|" + i.MatchKey)).ToList();
        if (toInsert.Any())
        {
            fsql.Insert(toInsert).ExecuteAffrows();
            Console.WriteLine($"[DB] 知识库新增 {toInsert.Count} 条");
        }
    }

    private static void InsertTermIfNotExists(IFreeSql fsql, List<TerminologyStandard> items)
    {
        var existing = fsql.Select<TerminologyStandard>().ToList(t => t.StandardTerm);
        var existingSet = existing.ToHashSet();
        var toInsert = items.Where(i => !existingSet.Contains(i.StandardTerm)).ToList();
        if (toInsert.Any())
        {
            fsql.Insert(toInsert).ExecuteAffrows();
            Console.WriteLine($"[DB] 术语标准新增 {toInsert.Count} 条");
        }
    }

    // ══════════════════════════════════════════════
    //  评分维度
    // ══════════════════════════════════════════════

    private static void SeedScoreDimensions(IFreeSql fsql)
    {
        if (fsql.Select<ScoreDimension>().Any()) return;
        fsql.Insert(new[]
        {
            new ScoreDimension { DimensionCode = "normative",    DimensionName = "规范性",   DefaultWeight = 30, Description = "资料完整性、解剖术语规范、标点符号、单位格式",               IsActive = true, SortOrder = 1, CreatedAt = DateTime.Now },
            new ScoreDimension { DimensionCode = "completeness", DimensionName = "全面性",   DefaultWeight = 30, Description = "病灶描述完整、历史对比、增强期相描述、随访建议",             IsActive = true, SortOrder = 2, CreatedAt = DateTime.Now },
            new ScoreDimension { DimensionCode = "logic",        DimensionName = "逻辑性",   DefaultWeight = 25, Description = "部位/方位/性别/数值逻辑、前后一致性",                      IsActive = true, SortOrder = 3, CreatedAt = DateTime.Now },
            new ScoreDimension { DimensionCode = "timeliness",   DimensionName = "及时性",   DefaultWeight = 15, Description = "报告完成时效、审核时效",                                   IsActive = true, SortOrder = 4, CreatedAt = DateTime.Now },
        }).ExecuteAffrows();
        JSBaseLogs.Info("评分维度初始化完成", "system");
    }

    // ══════════════════════════════════════════════
    //  知识库（基于 2025 专家共识 + 北京地标全面扩充）
    // ══════════════════════════════════════════════

    private static void SeedKnowledgeBase(IFreeSql fsql)
    {
        var now = DateTime.Now;
        var items = new List<KnowledgeBase>();

        void Add(string cat, string key, string val, string desc = "", string sev = "warning", int order = 0)
        {
            items.Add(new KnowledgeBase
            {
                CategoryCode = cat, MatchKey = key, MatchValue = val,
                Description = desc, Severity = sev, IsActive = true,
                SortOrder = order, CreatedAt = now, UpdatedAt = now
            });
        }

        // ── 性别排除：女性部位（男性不应出现） ──────────
        foreach (var kw in new[] {
            "子宫", "宫颈", "卵巢", "输卵管", "阴道", "子宫内膜", "附件", "乳腺", "乳房",
            "宫腔", "宫颈管", "卵泡", "黄体", "前庭大腺", "阴唇", "处女膜",
            "子宫肌瘤", "卵巢囊肿", "子宫腺肌症", "输卵管积水"
        })
            Add("gender_exclude_female", kw, J(new[] { "男", "未知" }),
                $"男性患者不应出现「{kw}」", "critical", 1);

        // ── 性别排除：男性部位（女性不应出现） ──────────
        foreach (var kw in new[] {
            "前列腺", "精囊", "睾丸", "附睾", "阴囊", "阴茎", "精索",
            "输精管", "射精管", "龟头", "包皮", "前列腺增生", "睾丸鞘膜积液"
        })
            Add("gender_exclude_male", kw, J(new[] { "女", "未知" }),
                $"女性患者不应出现「{kw}」", "critical", 1);

        // ── 部位映射：放射科 CT ──────────────────────
        Add("body_part_area_map", "头颅CT", J(new[]{ "颅脑","大脑","小脑","脑干","颅骨","蝶鞍","丘脑","基底节","侧脑室","蛛网膜","额叶","顶叶","颞叶","枕叶","岛叶" }));
        Add("body_part_area_map", "颈部CT", J(new[]{ "颈部","甲状腺","气管","食管","颈椎","淋巴结","软组织","颈动脉","颈静脉" }));
        Add("body_part_area_map", "胸部CT", J(new[]{ "肺","纵隔","心","胸廓","气管","支气管","胸膜","主动脉","肺动脉","冠状动脉","肋骨","肺门","叶间裂" }));
        Add("body_part_area_map", "腹部CT", J(new[]{ "肝","胆","胰","脾","肾","胃","肠","腹腔","腹膜","肾上腺","门静脉","下腔静脉","腹主动脉","腹腔干","肠系膜" }));
        Add("body_part_area_map", "盆腔CT", J(new[]{ "膀胱","子宫","卵巢","前列腺","直肠","乙状结肠","盆腔淋巴结","精囊","髂血管" }));
        Add("body_part_area_map", "脊柱CT", J(new[]{ "椎体","椎弓","椎板","椎间盘","椎管","棘突","横突","椎间孔","小关节" }));
        Add("body_part_area_map", "四肢CT", J(new[]{ "骨皮质","骨小梁","骨髓腔","关节面","关节间隙","软组织" }));
        Add("body_part_area_map", "上腹部CT", J(new[]{ "肝","胆","胰","脾","肾","胃","十二指肠","肾上腺","门静脉","下腔静脉" }));
        Add("body_part_area_map", "下腹部CT", J(new[]{ "小肠","结肠","盲肠","阑尾","膀胱","输尿管","髂血管","腹腔干" }));
        Add("body_part_area_map", "胸部高分辨CT", J(new[]{ "肺","支气管","细支气管","肺间质","小叶间隔","肺小叶","胸膜" }));
        Add("body_part_area_map", "CTA头颈", J(new[]{ "颈总动脉","颈内动脉","颈外动脉","椎动脉","大脑中动脉","大脑前动脉","大脑后动脉","基底动脉","Willis环","斑块","狭窄" }));
        Add("body_part_area_map", "CTA胸腹主动脉", J(new[]{ "主动脉","胸主动脉","腹主动脉","髂动脉","腹腔干","肠系膜上动脉","肾动脉","夹层","动脉瘤" }));
        Add("body_part_area_map", "CTA下肢", J(new[]{ "髂动脉","股动脉","腘动脉","胫前动脉","胫后动脉","腓动脉","足背动脉","狭窄","闭塞" }));

        // ── 部位映射：放射科 MRI ─────────────────────
        Add("body_part_area_map", "头部MRI", J(new[]{ "颅脑","大脑","小脑","脑干","颅骨","蝶鞍","丘脑","基底节","脑室","脑膜","垂体","额叶","顶叶","颞叶","枕叶","胼胝体","内囊","外囊" }));
        Add("body_part_area_map", "颈部MRI", J(new[]{ "颈部","脊髓","椎间盘","椎体","软组织","淋巴结","喉咽","气管" }));
        Add("body_part_area_map", "腰椎MRI", J(new[]{ "腰椎","椎体","椎间盘","椎管","马尾","神经根","硬膜囊","黄韧带" }));
        Add("body_part_area_map", "颈椎MRI", J(new[]{ "颈椎","椎体","椎间盘","椎管","脊髓","神经根","寰枢关节" }));
        Add("body_part_area_map", "胸椎MRI", J(new[]{ "胸椎","椎体","椎间盘","椎管","脊髓","后纵韧带" }));
        Add("body_part_area_map", "肩关节MRI", J(new[]{ "肱骨头","关节盂","肩袖","冈上肌腱","肩峰","关节软骨","盂唇","肱二头肌腱" }));
        Add("body_part_area_map", "膝关节MRI", J(new[]{ "股骨髁","胫骨平台","髌骨","半月板","交叉韧带","侧副韧带","关节软骨","腘窝","髌韧带","关节囊" }));
        Add("body_part_area_map", "髋关节MRI", J(new[]{ "股骨头","髋臼","关节软骨","关节囊","圆韧带","髋臼盂唇" }));
        Add("body_part_area_map", "踝关节MRI", J(new[]{ "距骨","胫骨远端","腓骨远端","韧带","关节软骨","跟骨","足舟骨","距腓韧带" }));
        Add("body_part_area_map", "腕关节MRI", J(new[]{ "舟骨","月骨","三角骨","桡骨远端","尺骨远端","TFCC","腕管","正中神经" }));
        Add("body_part_area_map", "肘关节MRI", J(new[]{ "肱骨远端","桡骨头","尺骨鹰嘴","侧副韧带","肱二头肌腱","肱三头肌腱" }));
        Add("body_part_area_map", "盆腔MRI", J(new[]{ "膀胱","子宫","宫颈","卵巢","前列腺","直肠","乙状结肠","盆腔淋巴结","精囊" }));
        Add("body_part_area_map", "直肠MRI", J(new[]{ "直肠","肛管","直肠系膜筋膜","直肠周围脂肪","髂内淋巴结","肠壁","浆膜" }));
        Add("body_part_area_map", "前列腺MRI", J(new[]{ "前列腺","外周带","移行带","中央带","精囊","膀胱颈","包膜","神经血管束" }));
        Add("body_part_area_map", "肝脏MRI", J(new[]{ "肝","肝右叶","肝左叶","尾状叶","肝段","S1","S2","S3","S4","S5","S6","S7","S8","胆管","门静脉","肝静脉" }));
        Add("body_part_area_map", "垂体MRI", J(new[]{ "垂体","腺垂体","神经垂体","垂体柄","鞍区","蝶鞍","海绵窦","视交叉" }));
        Add("body_part_area_map", "内耳MRI", J(new[]{ "耳蜗","前庭","半规管","内听道","蜗神经","前庭神经","面神经" }));
        Add("body_part_area_map", "MRA头部", J(new[]{ "大脑中动脉","大脑前动脉","大脑后动脉","颈内动脉","基底动脉","椎动脉","Willis环","动脉瘤","狭窄" }));

        // ── 部位映射：放射科 DR ──────────────────────
        Add("body_part_area_map", "胸部DR", J(new[]{ "肺","心","纵隔","胸廓","肋骨","膈肌","肋膈角","肺门","气管" }));
        Add("body_part_area_map", "腹部DR", J(new[]{ "胃","肠","膈肌","腹脂线","腰椎","肾轮廓","腰大肌" }));
        Add("body_part_area_map", "脊柱DR", J(new[]{ "椎体","椎间隙","棘突","横突","生理曲度" }));
        Add("body_part_area_map", "四肢DR", J(new[]{ "骨干","骨骺","关节面","骨皮质","骨小梁","干骺端" }));
        Add("body_part_area_map", "骨盆DR", J(new[]{ "髂骨","耻骨","坐骨","骶髂关节","髋关节","股骨头","股骨颈" }));
        Add("body_part_area_map", "头颅DR", J(new[]{ "颅骨","颅缝","蝶鞍","鼻窦","乳突","下颌骨" }));
        Add("body_part_area_map", "口腔全景", J(new[]{ "上颌骨","下颌骨","牙齿","牙槽骨","颞下颌关节","下颌管" }));

        // ── 部位映射：超声科 ────────────────────────
        Add("body_part_area_map", "腹部超声", J(new[]{ "肝","胆","胰","脾","肾","胆囊","胆总管","门静脉","肝内胆管","胰腺","腹腔干","腹主动脉" }));
        Add("body_part_area_map", "心脏超声", J(new[]{ "左心室","右心室","左心房","右心房","室间隔","主动脉","肺动脉","二尖瓣","三尖瓣","心包","心功能","EF","肺动脉瓣","主动脉瓣" }));
        Add("body_part_area_map", "甲状腺超声", J(new[]{ "甲状腺左叶","甲状腺右叶","峡部","甲状腺实质","包膜","颈部淋巴结" }));
        Add("body_part_area_map", "乳腺超声", J(new[]{ "乳腺","腺体","导管","乳头","乳晕","Cooper韧带","腋窝淋巴结","胸肌" }));
        Add("body_part_area_map", "妇科超声", J(new[]{ "子宫","宫颈","卵巢","附件","子宫内膜","肌层","宫腔","盆腔积液","输卵管" }));
        Add("body_part_area_map", "产科超声", J(new[]{ "胎儿","胎盘","羊水","脐动脉","胎心","胎位","双顶径","股骨长","头围","腹围","NT" }));
        Add("body_part_area_map", "泌尿系超声", J(new[]{ "肾","输尿管","膀胱","前列腺","精囊","残余尿","肾盂" }));
        Add("body_part_area_map", "血管超声", J(new[]{ "颈动脉","椎动脉","下肢动脉","下肢静脉","内中膜","斑块","血流","血栓","瓣膜功能" }));
        Add("body_part_area_map", "浅表超声", J(new[]{ "皮下","肌层","筋膜","淋巴结","肿块","囊性","实性","钙化" }));
        Add("body_part_area_map", "阴囊超声", J(new[]{ "睾丸","附睾","精索","鞘膜","阴囊壁","睾丸纵隔" }));

        // ── 部位映射：内镜中心 ────────────────────────
        Add("body_part_area_map", "胃镜", J(new[]{ "食管","贲门","胃底","胃体","胃窦","幽门","十二指肠球部","十二指肠降部","黏膜","齿状线" }));
        Add("body_part_area_map", "结肠镜", J(new[]{ "回盲部","升结肠","横结肠","降结肠","乙状结肠","直肠","结肠黏膜","肠腔","阑尾开口","回盲瓣" }));
        Add("body_part_area_map", "ERCP", J(new[]{ "十二指肠乳头","胆总管","胰管","肝内胆管","胆囊管","Oddi括约肌","胆囊","结石","狭窄" }));
        Add("body_part_area_map", "支气管镜", J(new[]{ "气管","隆突","左主支气管","右主支气管","叶支气管","段支气管","黏膜","管腔" }));
        Add("body_part_area_map", "膀胱镜", J(new[]{ "膀胱","尿道","输尿管口","膀胱三角","黏膜","膀胱颈" }));
        Add("body_part_area_map", "超声胃镜", J(new[]{ "食管壁","胃壁","十二指肠壁","黏膜下层","固有肌层","浆膜","纵隔淋巴结","胰腺","胆管" }));

        // ── 部位映射：其他检查 ───────────────────────
        Add("body_part_area_map", "钼靶", J(new[]{ "乳腺","腺体","导管","肿块","钙化","结构扭曲","不对称致密","腋窝淋巴结","乳头" }));
        Add("body_part_area_map", "PET-CT", J(new[]{ "全身","颅脑","肺","肝","骨骼","淋巴结","SUVmax","代谢","摄取" }));
        Add("body_part_area_map", "骨密度", J(new[]{ "腰椎","股骨颈","髋关节","桡骨远端","T值","Z值","骨密度" }));
        Add("body_part_area_map", "DSA", J(new[]{ "血管","动脉","狭窄","闭塞","夹层","动脉瘤","侧支循环","栓塞","支架" }));
        Add("body_part_area_map", "SPECT", J(new[]{ "骨骼显像","心肌灌注","肾动态显像","甲状腺显像","肺灌注","脑血流" }));

        // ── 年龄排除 ──────────────────────────────
        Add("age_exclude", "脑梗灶", J(new[]{ "0-12" }), "0-12岁出现脑梗灶应确认", "error", 3);
        Add("age_exclude", "脑白质疏松", J(new[]{ "0-20" }), "0-20岁出现脑白质疏松应确认", "warning", 3);
        Add("age_exclude", "骨质疏松", J(new[]{ "0-20" }), "0-20岁出现骨质疏松应确认", "error", 3);
        Add("age_exclude", "前列腺增生", J(new[]{ "0-30" }), "0-30岁出现前列腺增生应确认", "error", 3);
        Add("age_exclude", "退行性变", J(new[]{ "0-20" }), "0-20岁出现退行性变应确认", "warning", 3);
        Add("age_exclude", "骨质增生", J(new[]{ "0-18" }), "0-18岁出现骨质增生应确认", "warning", 3);
        Add("age_exclude", "恶性肿瘤", J(new[]{ "0-2" }), "0-2岁出现恶性肿瘤应确认", "error", 3);
        Add("age_exclude", "肝硬化", J(new[]{ "0-10" }), "0-10岁出现肝硬化应确认", "error", 3);
        Add("age_exclude", "冠状动脉硬化", J(new[]{ "0-15" }), "0-15岁出现冠状动脉硬化应确认", "error", 3);
        Add("age_exclude", "颈椎病", J(new[]{ "0-18" }), "0-18岁出现颈椎病应确认", "warning", 3);
        Add("age_exclude", "腰椎间盘突出", J(new[]{ "0-15" }), "0-15岁出现腰椎间盘突出应确认", "warning", 3);
        Add("age_exclude", "骨髓瘤", J(new[]{ "0-20" }), "0-20岁出现骨髓瘤应确认", "error", 3);
        Add("age_exclude", "前列腺癌", J(new[]{ "0-35" }), "0-35岁出现前列腺癌应确认", "error", 3);
        Add("age_exclude", "宫内膜癌", J(new[]{ "0-25" }), "0-25岁出现子宫内膜癌应确认", "error", 3);
        Add("age_exclude", "帕金森病", J(new[]{ "0-30" }), "0-30岁出现帕金森病应确认", "warning", 3);
        Add("age_exclude", "动脉粥样硬化", J(new[]{ "0-15" }), "0-15岁出现动脉粥样硬化应确认", "error", 3);
        Add("age_exclude", "脑萎缩", J(new[]{ "0-30" }), "0-30岁出现脑萎缩应确认", "warning", 3);
        Add("age_exclude", "骨关节病", J(new[]{ "0-18" }), "0-18岁出现骨关节病应确认", "warning", 3);
        Add("age_exclude", "子宫肌瘤", J(new[]{ "0-15" }), "0-15岁出现子宫肌瘤应确认", "warning", 3);
        Add("age_exclude", "白内障", J(new[]{ "0-30" }), "0-30岁出现白内障应确认", "warning", 3);

        // ── 设备关键词冲突 ──────────────────────────
        Add("exam_device_keywords", "CT", J(new[]{ "MRI示","MR示","磁共振示","MRI平扫","MRI增强","磁共振平扫","T1WI","T2WI","DWI","FLAIR" }), "CT检查不应出现MRI描述", "error", 4);
        Add("exam_device_keywords", "MRI", J(new[]{ "CT示","CT平扫","CT增强","CT扫描","X线片","X光片","三维重建CT","CTA示" }), "MRI检查不应出现CT描述", "error", 4);
        Add("exam_device_keywords", "DR", J(new[]{ "CT示","MRI示","MR示","CT平扫","MRI平扫","磁共振示","CT值","增强扫描","CTA" }), "DR检查不应出现CT/MRI描述", "error", 4);
        Add("exam_device_keywords", "超声", J(new[]{ "CT示","MRI示","MR示","CT平扫","MRI平扫","X线片","磁共振示","CT值","CT增强" }), "超声检查不应出现CT/MRI/X线描述", "error", 4);
        Add("exam_device_keywords", "胃镜", J(new[]{ "CT示","超声示","MRI示","X线","磁共振示","CT增强","CT平扫" }), "胃镜检查不应出现其他设备描述", "error", 4);
        Add("exam_device_keywords", "结肠镜", J(new[]{ "CT示","超声示","MRI示","X线","磁共振示" }), "结肠镜检查不应出现其他设备描述", "error", 4);
        Add("exam_device_keywords", "钼靶", J(new[]{ "CT示","MRI示","超声示","磁共振示","CT增强","CT平扫","CT值" }), "钼靶检查不应出现其他设备描述", "error", 4);
        Add("exam_device_keywords", "DSA", J(new[]{ "CT示","MRI示","超声示","CT增强","CT平扫" }), "DSA检查不应出现其他设备描述", "error", 4);
        Add("exam_device_keywords", "核医学", J(new[]{ "CT示","MRI示","超声示","CT增强" }), "核医学检查不应出现其他设备描述", "error", 4);

        // ── 检查方式冲突 ──────────────────────────
        Add("scan_type_map", "平扫", J(new[]{ "强化","增强","明显强化","明显增强","中度强化","轻度强化","不均匀强化","环形强化","进行性强化","延迟强化","渐进性强化","快进快出","快进慢出","动脉期强化","门脉期强化","延迟期强化" }), "平扫检查不应出现强化描述", "error", 4);

        // ── 危急征象关键词 ──────────────────────────
        Add("critical_sign_keywords", "critical_sign",
            J(new[]{ "气胸","张力性气胸","心包填塞","心脏压塞","主动脉夹层","主动脉破裂","主动脉壁间血肿",
               "颅内出血","脑出血","硬膜外血肿","硬膜下血肿","蛛网膜下腔出血","脑疝","脑疝形成",
               "急性肺栓塞","大面积肺栓塞","肺栓塞",
               "急性心梗","心肌梗死","急性冠状动脉综合征",
               "肝破裂","脾破裂","肾破裂","腹腔积血",
               "宫外孕","异位妊娠破裂","异位妊娠",
               "消化道穿孔","肠穿孔","胃穿孔",
               "急性胰腺炎重症","重症胰腺炎","坏死性胰腺炎",
               "化脓性胆管炎","急性化脓性胆管炎",
               "急性主动脉综合征","穿透性主动脉溃疡",
               "肠系膜缺血","肠系膜动脉栓塞","肠系膜上动脉夹层",
               "支气管断裂","气管断裂",
               "心包积液伴填塞","大量心包积液",
               "活动性出血","活动性渗血",
               "脊髓压迫","急性脊髓压迫症",
               "颈动脉夹层","椎动脉夹层",
               "颅内动脉瘤破裂","动脉瘤破裂",
               "急性肾损伤","急性肾功能衰竭",
               "上腔静脉综合征",
               "睾丸扭转",
               "视网膜中央动脉阻塞" }),
            "危急征象触发关键词", "critical", 5);

        // ── 方位词 ────────────────────────────────
        Add("direction_positive", "positive", J(new[]{ "左侧","左叶","左侧壁","左肺","左肾","左附件","左上","左下","左前","左后","左肺上叶","左肺下叶","左乳","左肝" }), "", "warning", 6);
        Add("direction_negative", "negative", J(new[]{ "右侧","右叶","右侧壁","右肺","右肾","右附件","右上","右下","右前","右后","右肺上叶","右肺中叶","右肺下叶","右乳","右肝" }), "", "warning", 6);

        // ── 单位校验正则 ────────────────────────────
        Add("unit_pattern", "pattern", @"\d+\.?\d*\s*(cm|mm|m)\s*[×xX×*]\s*\d+\.?\d*\s*(cm|mm|m)", "尺寸描述单位校验", "warning", 7);

        // ── 需要 RADS 分类的检查类型 ──────────────────
        Add("rads_require_types", "乳腺", J(new[]{ "BI-RADS" }), "乳腺检查需标注BI-RADS", "error", 8);
        Add("rads_require_types", "钼靶", J(new[]{ "BI-RADS" }), "钼靶检查需标注BI-RADS", "error", 8);
        Add("rads_require_types", "乳腺超声", J(new[]{ "BI-RADS" }), "乳腺超声检查需标注BI-RADS", "error", 8);
        Add("rads_require_types", "前列腺", J(new[]{ "PI-RADS" }), "前列腺检查需标注PI-RADS", "error", 8);
        Add("rads_require_types", "前列腺MRI", J(new[]{ "PI-RADS" }), "前列腺MRI检查需标注PI-RADS", "error", 8);
        Add("rads_require_types", "肝脏", J(new[]{ "LI-RADS" }), "肝脏检查需标注LI-RADS", "error", 8);
        Add("rads_require_types", "肝脏MRI", J(new[]{ "LI-RADS" }), "肝脏MRI检查需标注LI-RADS", "error", 8);
        Add("rads_require_types", "甲状腺", J(new[]{ "TI-RADS" }), "甲状腺检查需标注TI-RADS", "error", 8);
        Add("rads_require_types", "甲状腺超声", J(new[]{ "TI-RADS" }), "甲状腺超声检查需标注TI-RADS", "error", 8);
        Add("rads_require_types", "肺结节", J(new[]{ "Lung-RADS" }), "肺结节筛查需标注Lung-RADS", "warning", 8);
        Add("rads_require_types", "胸部CT筛查", J(new[]{ "Lung-RADS" }), "胸部CT筛查需标注Lung-RADS", "warning", 8);

        // ── 新增：必填字段清单 ───────────────────────
        Add("required_fields", "patient", J(new[]{ "PatientName","PatientGender","PatientAge","AccessionNo","ExamPart","ExamDevice" }),
            "患者基本信息必填字段", "error", 10);

        // ── 新增：检查技术必填项 ─────────────────────
        Add("exam_technique_required", "technique", J(new[]{ "扫描方式","层厚","对比剂","序列","方位" }),
            "检查技术描述必填项", "warning", 11);

        // ── 新增：需要随访建议的诊断关键词 ──────────
        Add("followup_indicators", "followup", J(new[]{
            "建议随访","建议复查","短期随访","定期复查","进一步检查","建议活检",
            "恶性不除外","不除外恶性","可疑恶性","恶性肿瘤可能","占位性病变",
            "建议增强","建议MRI检查","建议CT检查","建议穿刺"
        }), "需要随访建议的诊断关键词", "warning", 12);

        // ── 新增：阳性诊断词（结论中出现即认为给出诊断） ───
        Add("positive_diagnosis_words", "diagnosis", J(new[]{
            "考虑","符合","提示","可能","癌","瘤","结节","肿块","占位","病变",
            "骨折","血肿","积液","炎症","感染","囊肿","结石","钙化",
            "动脉瘤","夹层","狭窄","闭塞","栓塞","血栓","出血",
            "梗塞","坏死","水肿","萎缩","增生","硬化","纤维化",
            "转移","复发","浸润","侵犯","扩散","增大","增多"
        }), "阳性诊断关键词", "info", 13);

        InsertIfNotExists(fsql, items);
    }

    // ══════════════════════════════════════════════
    //  术语标准（基于 2025 专家共识大幅扩充）
    //  分为：解剖术语(anatomy) / 影像征象(imaging_sign) / 操作规范(procedure)
    // ══════════════════════════════════════════════

    private static void SeedTerminology(IFreeSql fsql)
    {
        var items = new List<TerminologyStandard>();

        void Add(string term, string cat, string[] nonStd)
        {
            items.Add(new TerminologyStandard
            {
                StandardTerm = term, Category = cat,
                NonStandardTerms = JsonConvert.SerializeObject(nonStd),
                IsActive = true, CreatedAt = DateTime.Now
            });
        }

        // ── 解剖术语规范（anatomy）────────────────────
        Add("右肺上叶", "anatomy", new[]{ "右上肺","右肺上部","右上叶","右肺上" });
        Add("右肺中叶", "anatomy", new[]{ "右中肺","右肺中部","右中叶","右肺中" });
        Add("右肺下叶", "anatomy", new[]{ "右下肺","右肺下部","右下叶","右肺下" });
        Add("左肺上叶", "anatomy", new[]{ "左上肺","左肺上部","左上叶","左肺上" });
        Add("左肺下叶", "anatomy", new[]{ "左下肺","左肺下部","左下叶","左肺下" });
        Add("甲状腺左叶", "anatomy", new[]{ "甲状腺左","左侧甲状腺","左甲","左甲状腺叶" });
        Add("甲状腺右叶", "anatomy", new[]{ "甲状腺右","右侧甲状腺","右甲","右甲状腺叶" });
        Add("肝右叶", "anatomy", new[]{ "肝脏右叶","肝右","右肝","右半肝" });
        Add("肝左叶", "anatomy", new[]{ "肝脏左叶","肝左","左肝","左半肝" });
        Add("肝尾状叶", "anatomy", new[]{ "肝尾叶","尾叶肝","肝尾" });
        Add("肝S5段", "anatomy", new[]{ "肝脏5段","S5肝段","肝五段" });
        Add("肝S6段", "anatomy", new[]{ "肝脏6段","S6肝段","肝六段" });
        Add("肝S7段", "anatomy", new[]{ "肝脏7段","S7肝段","肝七段" });
        Add("肝S8段", "anatomy", new[]{ "肝脏8段","S8肝段","肝八段" });
        Add("肾盂", "anatomy", new[]{ "肾孟","肾孟部","肾盂部" });
        Add("肾盏", "anatomy", new[]{ "肾盏","肾盏部" });
        Add("输尿管", "anatomy", new[]{ "输尿管","输柰管","输尿官" });
        Add("胰腺", "anatomy", new[]{ "胰线","胰線","胰" });
        Add("胆囊", "anatomy", new[]{ "胆囊","胆琅" });
        Add("阑尾", "anatomy", new[]{ "烂尾","兰尾","阑尾" });
        Add("腋杖", "anatomy", new[]{ "拐杖","腋下拐","腋拐" });
        Add("髂骨", "anatomy", new[]{ "胳骨","髋骨","髂" });
        Add("骶骨", "anatomy", new[]{ "底骨","胝骨","骶" });
        Add("额叶", "anatomy", new[]{ "额页","额叶" });
        Add("颞叶", "anatomy", new[]{ "颞页","颞叶","镊叶" });
        Add("枕叶", "anatomy", new[]{ "枕页","枕叶" });
        Add("顶叶", "anatomy", new[]{ "顶页","顶叶" });
        Add("胼胝体", "anatomy", new[]{ "骈胝体","胼眂体","胼眡体" });
        Add("垂体", "anatomy", new[]{ "垂休","锤体","垂" });
        Add("蝶鞍", "anatomy", new[]{ "蝶安","蝶案" });
        Add("海绵窦", "anatomy", new[]{ "海棉窦","海绵豆" });
        Add("椎体", "anatomy", new[]{ "椎休","锥体" });
        Add("硬膜囊", "anatomy", new[]{ "硬膜嚢","硬瞙囊" });
        Add("蛛网膜", "anatomy", new[]{ "蛛网瞙","蛛网膜" });
        Add("贲门", "anatomy", new[]{ "喷门","贲門" });
        Add("幽门", "anatomy", new[]{ "幽門","悠门" });
        Add("十二指肠", "anatomy", new[]{ "十二脂肠","十二指肠" });
        Add("回盲部", "anatomy", new[]{ "回盲","回肓部","回肓" });
        Add("纵隔", "anatomy", new[]{ "纵膈","综隔","纵隔膜" });
        Add("横膈", "anatomy", new[]{ "横隔","膈肌" });
        Add("腋窝", "anatomy", new[]{ "腋下","腋部","叶窝" });
        Add("腹膜", "anatomy", new[]{ "腹瞙","腹膜" });
        Add("胸膜", "anatomy", new[]{ "胸瞙","胸膜" });
        Add("肾皮质", "anatomy", new[]{ "肾脏皮质","肾皮贽" });
        Add("肾髓质", "anatomy", new[]{ "肾脏髓质","肾腄质" });
        Add("脾脏", "anatomy", new[]{ "痞脏","皮脏","啤脏" });
        Add("前列腺", "anatomy", new[]{ "前列线","前列" });
        Add("精囊", "anatomy", new[]{ "精囊腺","精囊腺体" });
        Add("附睾", "anatomy", new[]{ "付睾","附塞","副睾" });

        // ── 影像征象规范（imaging_sign）─────────────
        Add("低密度灶", "imaging_sign", new[]{ "低密谋灶","低密灶","低密度影","低密影" });
        Add("高密度灶", "imaging_sign", new[]{ "高密灶","高密度影","高密影","致密灶" });
        Add("等密度", "imaging_sign", new[]{ "等密谋","等密","同等密度" });
        Add("轨道征", "imaging_sign", new[]{ "轨道症","轨迹征","导水管征" });
        Add("毛刺征", "imaging_sign", new[]{ "刺征","毛刺症","锯齿征","毛刺样" });
        Add("分叶征", "imaging_sign", new[]{ "分页症","分叶症","分叶样" });
        Add("胸膜凹陷征", "imaging_sign", new[]{ "胸膜牵拉征","胸膜凹陷症","胸膜尾征","兔耳征" });
        Add("血管集束征", "imaging_sign", new[]{ "血管聚集征","集束征","血管聚集" });
        Add("空泡征", "imaging_sign", new[]{ "空泡症","含气腔","空洞征" });
        Add("充气支气管征", "imaging_sign", new[]{ "支气管充气征","含气支气管征","空气支气管征" });
        Add("磨玻璃密度", "imaging_sign", new[]{ "磨玻璃样","毛玻璃密度","毛玻璃影","GGO" });
        Add("钙化", "imaging_sign", new[]{ "钙化灶","钙化点","钙化斑","钙化斑块" });
        Add("坏死", "imaging_sign", new[]{ "坏死灶","坏死区","坏列","坏列灶" });
        Add("囊变", "imaging_sign", new[]{ "囊性变","囊性变化","囊变区" });
        Add("异常强化", "imaging_sign", new[]{ "不典型强化","增强程度异常","非典型强化" });
        Add("环形强化", "imaging_sign", new[]{ "环状强化","圈状强化","轮状强化" });
        Add("不均匀强化", "imaging_sign", new[]{ "不均一强化","非均匀强化","不均匀增强" });
        Add("渐进性强化", "imaging_sign", new[]{ "进行性强化","逐渐强化","慢慢强化" });
        Add("快进快出", "imaging_sign", new[]{ "快进快出型","速升速降","快进型" });
        Add("快进慢出", "imaging_sign", new[]{ "快进慢出型","快进迟出","快进型" });
        Add("胆囊结石", "imaging_sign", new[]{ "胆结石","胆囊内结石","石头" });
        Add("胆总管结石", "imaging_sign", new[]{ "胆总管结石","胆管石头","总胆管结石" });
        Add("肾结石", "imaging_sign", new[]{ "肾脏结石","肾内结石","肾石头" });
        Add("输尿管结石", "imaging_sign", new[]{ "输尿管结石","输尿管石头" });
        Add("脑梗死", "imaging_sign", new[]{ "脑梗塞","脑梗","缺血灶","缺血性" });
        Add("脑出血", "imaging_sign", new[]{ "脑内出血","颅内血肿","脑溢血" });
        Add("蛛网膜下腔出血", "imaging_sign", new[]{ "蛛网膜出血","蛛血","蛛膜下出血" });
        Add("硬膜下血肿", "imaging_sign", new[]{ "硬膜下出血","硬膜下积血","硬膜下血肿" });
        Add("硬膜外血肿", "imaging_sign", new[]{ "硬膜外出血","硬膜外积血" });
        Add("肺气肿", "imaging_sign", new[]{ "肺气肿","肺大疱","肺大泡" });
        Add("胸腔积液", "imaging_sign", new[]{ "胸水","胸积液","胸腹腔积液" });
        Add("心包积液", "imaging_sign", new[]{ "心包积液","心包内液","心囊积液" });
        Add("腹腔积液", "imaging_sign", new[]{ "腹水","腹积液","腹腔内积液" });
        Add("盆腔积液", "imaging_sign", new[]{ "盆腔积液","盆水","盆腔内液" });
        Add("肝囊肿", "imaging_sign", new[]{ "肝脏囊肿","肝内囊肿","肝囊性" });
        Add("肾囊肿", "imaging_sign", new[]{ "肾脏囊肿","肾内囊肿","肾囊性" });
        Add("脂肪肝", "imaging_sign", new[]{ "脂肪肝","肝脂肪变","肝脂" });
        Add("肝硬化", "imaging_sign", new[]{ "肝纤维化","硬变肝","肝硬" });
        Add("骨折", "imaging_sign", new[]{ "骨拆","骨裂痕","折骨" });
        Add("骨质疏松", "imaging_sign", new[]{ "骨松","骨质硫松","骨质量疏松" });
        Add("骨质增生", "imaging_sign", new[]{ "骨刺","骨赘","骨增生" });
        Add("椎间盘突出", "imaging_sign", new[]{ "椎间盘脱出","间盘突出","盘突出" });
        Add("椎间盘膨出", "imaging_sign", new[]{ "间盘膨出","盘膨出","椎间盘膨胀" });
        Add("淋巴结肿大", "imaging_sign", new[]{ "淋巴肿大","淋巴节增大","淋大" });
        Add("动脉粥样硬化", "imaging_sign", new[]{ "动脉硬化","粥样硬化","动脉斑块" });

        // ── 操作规范（procedure）───────────────────
        Add("CT平扫", "procedure", new[]{ "CT平扫+三维","CT平扫三维","CT平扫加三维" });
        Add("CT增强", "procedure", new[]{ "CT加强","CT增强扫描","增强CT" });
        Add("MRI平扫", "procedure", new[]{ "核磁平扫","磁共振平扫","MR平扫" });
        Add("MRI增强", "procedure", new[]{ "核磁增强","磁共振增强","MR增强","增强MRI" });
        Add("三维重建", "procedure", new[]{ "3D重建","三维重构","3维重建" });
        Add("多平面重建", "procedure", new[]{ "MPR","多个平面重建","多维重建" });
        Add("最大密度投影", "procedure", new[]{ "MIP","最大密度投射","密度投影" });

        InsertTermIfNotExists(fsql, items);
    }

    // ══════════════════════════════════════════════
    //  RADS 标准（扩展至 5 种分类标准）
    // ══════════════════════════════════════════════

    private static void SeedRadsStandards(IFreeSql fsql)
    {
        if (fsql.Select<RadsStandard>().Any()) return;

        void Add(string type, string grade, string name, string desc, string risk)
        {
            fsql.Insert(new RadsStandard
            {
                RadsType = type, Grade = grade, GradeName = name,
                Description = desc, MalignancyRisk = risk,
                IsActive = true,
                SortOrder = int.TryParse(grade[..1], out var n) ? n : 0,
                CreatedAt = DateTime.Now
            }).ExecuteAffrows();
        }

        // BI-RADS（乳腺）
        Add("BI-RADS", "0", "评估不完整", "需要额外影像评估", "");
        Add("BI-RADS", "1", "阴性", "无异常发现", "0%");
        Add("BI-RADS", "2", "良性发现", "明确良性", "0%");
        Add("BI-RADS", "3", "可能良性", "恶性概率极低，短期随访", ">0%且≤2%");
        Add("BI-RADS", "4a", "低度怀疑恶性", "需活检", ">2%且≤10%");
        Add("BI-RADS", "4b", "中度怀疑恶性", "需活检", ">10%且≤50%");
        Add("BI-RADS", "4c", "高度怀疑恶性", "需活检", ">50%且<95%");
        Add("BI-RADS", "5", "高度提示恶性", "高度提示恶性", "≥95%");
        Add("BI-RADS", "6", "已活检证实恶性", "已病理证实", "—");

        // TI-RADS（甲状腺）
        Add("TI-RADS", "1", "阴性", "无结节", "0%");
        Add("TI-RADS", "2", "良性", "囊性或海绵样", "0%");
        Add("TI-RADS", "3", "可能良性", "等回声或高回声实性结节", "<5%");
        Add("TI-RADS", "4", "可疑恶性", "低回声实性结节", "5%-80%");
        Add("TI-RADS", "5", "高度怀疑恶性", "低回声+微钙化+不规则边缘", ">80%");

        // PI-RADS（前列腺）
        Add("PI-RADS", "1", "极低度可疑", "无明显异常", "极低");
        Add("PI-RADS", "2", "低度可疑", "良性可能大", "低");
        Add("PI-RADS", "3", "中度可疑", "不确定", "中等");
        Add("PI-RADS", "4", "高度可疑", "可能恶性", "较高");
        Add("PI-RADS", "5", "极高可疑", "高度提示恶性", "极高");

        // LI-RADS（肝脏）
        Add("LI-RADS", "LR-1", "肯定良性", "明确良性", "0%");
        Add("LI-RADS", "LR-2", "可能良性", "良性可能大", "低");
        Add("LI-RADS", "LR-3", "中等可疑", "不确定", "中等");
        Add("LI-RADS", "LR-4", "可能恶性", "HCC可能", "较高");
        Add("LI-RADS", "LR-5", "肯定恶性", "HCC确定", "极高");
        Add("LI-RADS", "LR-M", "可能恶性非HCC", "恶性非肝细胞癌", "—");
        Add("LI-RADS", "LR-TIV", "静脉内肿瘤", "肿瘤侵犯静脉", "—");

        // Lung-RADS（肺结节）
        Add("Lung-RADS", "0", "不完整", "需既往影像对比", "");
        Add("Lung-RADS", "1", "阴性", "无结节或肯定良性钙化", "<1%");
        Add("Lung-RADS", "2", "良性表现", "良性表现或实性<6mm", "<1%");
        Add("Lung-RADS", "3", "可能良性", "短期随访", "1%-2%");
        Add("Lung-RADS", "4A", "可疑", "3-6个月随访", "5%-15%");
        Add("Lung-RADS", "4B", "高度可疑", "建议活检", ">15%");
        Add("Lung-RADS", "4X", "高度可疑伴其他特征", "建议活检", ">15%");

        JSBaseLogs.Info("RADS标准初始化完成 (5种分类标准)", "system");
    }

    // ── 辅助 ──────────────────────────────────────
    private static string J(string[] arr) => JsonConvert.SerializeObject(arr);
}
