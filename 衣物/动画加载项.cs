using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyMod.Clothing
{
    // 使用Harmony库对EquipmentTemplates类的CreateEquipmentDef方法进行补丁操作
    [HarmonyPatch(typeof(EquipmentTemplates), "CreateEquipmentDef")]
    internal class 动画加载项
    {
            // 后置补丁方法，在原始CreateEquipmentDef方法执行后调用
            public static void Postfix(EquipmentDef __result, string Anim, string Id)
            {
                // 从方法结果中获取KAnimFile对象（如果结果不为null）
                KAnimFile x = (__result != null) ? __result.Anim : null;
                // 标记变量，用于判断KAnimFile对象是否为null
                bool flag = x == null;
                // 如果KAnimFile对象为null，则记录警告日志
                if (flag)
                {
                    // 格式化输出警告信息，包含动画名称和装备ID
                    Debug.LogWarningFormat("(当在装备 {1} 上查找名为 {0} 的KAnim时)", new object[]
                    {
                    Anim,  // 第一个参数：动画名称
                    Id     // 第二个参数：装备ID
                    });
                }
            }
    }
}
