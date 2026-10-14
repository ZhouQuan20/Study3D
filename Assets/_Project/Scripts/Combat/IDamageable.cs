using UnityEngine;

namespace Study3D.Combat
{
    /// <summary>
    /// 通用受击契约接口（Interface Segregation & Decoupling）。
    /// 无论目标是敌人（Enemy）、玩家（Player）、木箱（WoodenBox）还是炸药桶（ExplosiveBarrel），
    /// 只要实现了该接口，攻击发起者（如 Projectile、MeleeAttack）都可以直接对其造成伤害，
    /// 无需关心具体目标的类型，杜绝大量 if-else / GetComponent 具体类的 面条代码。
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// 当前血量
        /// </summary>
        int CurrentHealth { get; }

        /// <summary>
        /// 最大血量
        /// </summary>
        int MaxHealth { get; }

        /// <summary>
        /// 是否已经死亡
        /// </summary>
        bool IsDead { get; }

        /// <summary>
        /// 承受伤害核心接口
        /// </summary>
        /// <param name=\damageAmount\>受到的伤害数值（非负整数）</param>
        /// <param name=\hitPoint\>命中点的世界空间坐标（可用于生成受击火花、击退方向计算等）</param>
        void TakeDamage(int damageAmount, Vector3 hitPoint = default);
    }
}