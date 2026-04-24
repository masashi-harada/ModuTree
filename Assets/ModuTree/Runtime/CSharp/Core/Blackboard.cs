using System.Collections.Generic;

namespace ModuTree.Runtime.Core
{
    /// <summary>
    /// ノード間でデータを共有するためのBlackboard。
    /// AIの判断結果と実行制御を疎結合に保つ。
    /// </summary>
    public class Blackboard
    {
        private readonly Dictionary<string, object> _data = new Dictionary<string, object>();

        // ─── 型安全API（BlackboardKey<T>使用） ──────────────

        public void Set<T>(BlackboardKey<T> key, T value)
            => _data[key.Name] = value;

        public T Get<T>(BlackboardKey<T> key)
        {
            if (_data.TryGetValue(key.Name, out var value) && value is T typed)
                return typed;
            return default;
        }

        public bool Has<T>(BlackboardKey<T> key)
            => _data.ContainsKey(key.Name);

        public void Remove<T>(BlackboardKey<T> key)
            => _data.Remove(key.Name);

        // ─── 文字列API（後方互換・内部用）──────────────────

        public void SetRaw(string key, object value)
            => _data[key] = value;

        public object GetRaw(string key)
            => _data.TryGetValue(key, out var v) ? v : null;

        public bool HasRaw(string key)
            => _data.ContainsKey(key);

        public void Clear() => _data.Clear();
    }
}