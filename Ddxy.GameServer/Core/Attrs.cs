using System;
using System.Collections;
using System.Collections.Generic;
using Ddxy.GameServer.Util;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Core
{
    public class Attrs : IEnumerable<KeyValuePair<AttrType, float>>, IDisposable
    {
        private Dictionary<AttrType, float> _dic;

        public int Count => _dic.Count;

        public Dictionary<AttrType, float>.KeyCollection Keys => _dic.Keys;

        public Dictionary<AttrType, float>.ValueCollection Values => _dic.Values;

        public Attrs()
        {
            _dic = new Dictionary<AttrType, float>();
        }

        public Attrs(int capacity)
        {
            _dic = new Dictionary<AttrType, float>(capacity);
        }

        public Attrs(IDictionary<AttrType, float> dic)
        {
            _dic = new Dictionary<AttrType, float>(dic);
        }

        public Attrs(IReadOnlyCollection<AttrPair> list)
        {
            _dic = new Dictionary<AttrType, float>(list.Count);
            foreach (var pair in list)
            {
                Add(pair.Key, pair.Value);
            }
        }

        public Attrs(string json)
        {
            _dic = new Dictionary<AttrType, float>();
            FromJson(json);
        }

        public float Get(AttrType attrType, float defValue = 0f)
        {
            if (!_dic.TryGetValue(attrType, out var value)) return defValue;
            return value;
        }

        public void Set(AttrType attrType, float value)
        {
            _dic[attrType] = value;
            if (value == 0) _dic.Remove(attrType);
        }

        public float SetPercent(AttrType attrType, float percent)
        {
            var val = MathF.Floor(Get(attrType) * percent);
            Set(attrType, val);
            return val;
        }

        public float Add(AttrType attrType, float value, bool nlt0 = false)
        {
            _dic.TryGetValue(attrType, out var v);
            v += value;
            if (nlt0 && v < 0) v = 0;
            _dic[attrType] = v;
            if (v == 0) _dic.Remove(attrType);
            return v;
        }

        public float AddPercent(AttrType attrType, float percent)
        {
            var val = MathF.Floor(Get(attrType) * (1 + percent));
            Set(attrType, val);
            return val;
        }

        public bool Has(AttrType attrType)
        {
            return _dic.ContainsKey(attrType);
        }

        public void Clear()
        {
            _dic.Clear();
        }

        public bool Remove(AttrType attrType)
        {
            return _dic.Remove(attrType);
        }

        public bool Remove(AttrType attrType, out float value)
        {
            return _dic.Remove(attrType, out value);
        }

        public void Dispose()
        {
            _dic.Clear();
            _dic = null;
        }

        public void CopyFrom(Attrs other)
        {
            if (other == null || other.Count == 0) return;
            foreach (var (k, v) in other)
            {
                Set(k, v);
            }
        }

        /// <summary>
        /// 序列化成Json字符串
        /// </summary>
        public string ToJson()
        {
            if (_dic == null || _dic.Count == 0) return string.Empty;

            var dic = new Dictionary<int, float>();
            foreach (var (k, v) in _dic)
            {
                if (v != 0) dic[(int) k] = v;
            }

            return dic.Count == 0 ? string.Empty : Json.SafeSerialize(dic);
        }

        /// <summary>
        /// 从json中反序列化
        /// </summary>
        public void FromJson(string json)
        {
            _dic.Clear();
            if (!string.IsNullOrWhiteSpace(json))
            {
                var dic = Json.SafeDeserialize<Dictionary<int, float>>(json);
                foreach (var (k, v) in dic)
                {
                    if (v != 0) _dic[(AttrType) k] = v;
                }
            }
        }

        /// <summary>
        /// 获取AttrPair集合
        /// </summary>
        public List<AttrPair> ToList()
        {
            var list = new List<AttrPair>(_dic.Count);
            foreach (var (k, v) in _dic)
            {
                if (v != 0) list.Add(new AttrPair {Key = k, Value = v});
            }

            return list;
        }

        public IEnumerator<KeyValuePair<AttrType, float>> GetEnumerator()
        {
            return _dic.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}