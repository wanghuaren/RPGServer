using System;
using System.Collections.Generic;

namespace Ddxy.GameServer.Core
{
    /// <summary>
    /// 单线程Id生成器
    /// </summary>
    public class IdGen<T> : IDisposable
    {
        private uint _id;
        public Dictionary<uint, T> All { get; private set; }

        public IdGen(int capacity)
        {
            _id = 0;
            All = new Dictionary<uint, T>(capacity);
        }

        /// <summary>
        /// 获取一个id, 不用的时候要记得回收
        /// </summary>
        public uint Gain(T value)
        {
            do
            {
                _id++;
                if (_id == 0) _id = 1;
            } while (All.ContainsKey(_id));

            // 标记占用
            All.Add(_id, value);
            return _id;
        }

        public uint Gain()
        {
            return Gain(default);
        }

        public void Use(uint id, T value)
        {
            All.Add(id, value);
        }

        /// <summary>
        /// 回收id
        /// </summary>
        public bool Recycle(uint id)
        {
            return All.Remove(id, out _);
        }

        /// <summary>
        /// 回收id
        /// </summary>
        public bool Recycle(uint id, out T t)
        {
            return All.Remove(id, out t);
        }

        /// <summary>
        /// 检查id是否存在
        /// </summary>
        public bool Exists(uint id)
        {
            return All.ContainsKey(id);
        }

        public T GetValue(uint id)
        {
            All.TryGetValue(id, out var val);
            return val;
        }

        public void Dispose()
        {
            _id = 0;
            All.Clear();
            All = null;
        }
    }
}