using System.Collections.Generic;
using UnityEngine;

namespace WordWars
{
    /// <summary>
    /// 极简对象池：模板克隆取用/归还。战斗中零 Instantiate/Destroy（GC 红线）。
    /// 模板保持非激活状态，克隆继承后按需激活。
    /// </summary>
    public class ViewPool<T> where T : Component
    {
        private readonly Transform root;
        private readonly Stack<T> free = new Stack<T>();

        public ViewPool(Transform parent)
        {
            root = new GameObject(typeof(T).Name + "Pool").transform;
            root.SetParent(parent, false);
        }

        public T Get(T template)
        {
            T item = free.Count > 0 ? free.Pop() : Object.Instantiate(template, root);
            item.gameObject.SetActive(true);
            return item;
        }

        public void Release(T item)
        {
            item.gameObject.SetActive(false);
            item.transform.SetParent(root, false);
            free.Push(item);
        }
    }
}
