namespace ModuTree.Runtime.Core
{
    /// <summary>
    /// 型安全なBlackboardキー。
    /// string直打ちによるキーのタイプミスをコンパイル時に検出する。
    /// </summary>
    public sealed class BlackboardKey<T>
    {
        public string Name { get; }
        public BlackboardKey(string name) => Name = name;
        public override string ToString() => Name;
    }
}