using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace TgBot
{
    public delegate ObjectType RetrieveObjectDelegate<IDType,ObjectType>(IDType id);
    public delegate bool IsValueNullDelegate<T>(T v);
    public class CachedObject<IDType,ObjectType>
    {
        RetrieveObjectDelegate<IDType,ObjectType> rdel;
        public CachedObject(RetrieveObjectDelegate<IDType,ObjectType> retrieveDelegate)
        {
            rdel = retrieveDelegate;
        }
        Dictionary<IDType, ObjectType> _cache=new Dictionary<IDType,ObjectType>();
        public ObjectType this[IDType id]
        {
            get
            {
                ObjectType ret;
                if (_cache.TryGetValue(id,out ret))
                    return ret;
                ret = rdel(id);
                _cache.Add(id, ret);
                return ret;
            }
        }
    }
    public interface ITransactionObject
    {
        void BeginTransaction();
        void CommitTransaction();
        void RollBackTransaction();
    }
    public abstract class CachedObjectBase<IDType, ObjectType>:ITransactionObject,IEnumerable<KeyValuePair<IDType,ObjectType>>
    {
        abstract class Transaction
        {
            public abstract void execute();
            public abstract void reverse();
        }
        class AddTransaction:Transaction
        {
            CachedObjectBase<IDType, ObjectType> parent;
            byte[] value;
            bool add;
            byte[] oldValue;
            IDType id;
            public AddTransaction(CachedObjectBase<IDType, ObjectType> parent, IDType id, ObjectType value)
            {
                this.parent = parent;
                this.id = id;
                this.value = parent.getBytes(value);
            }

            public override void execute()
            {
                if (parent._cache.ContainsKey(id))
                {
                    add = false;
                    oldValue = parent._cache[id];
                    parent._cache[id] = value;
                }
                else
                {
                    add = true;
                    parent._cache.Add(id, value);
                }
            }

            public override void reverse()
            {
                if (add)
                    parent._cache.Remove(id);
                else
                    parent._cache[id] = oldValue;
            }
        }
        class RemoveTransaction : Transaction
        {
            CachedObjectBase<IDType, ObjectType> parent;
            bool existed;
            byte[] oldValue;
            IDType id;
            public RemoveTransaction(CachedObjectBase<IDType, ObjectType> parent, IDType id)
            {
                this.parent = parent;
                this.id = id;
            }
            public override void execute()
            {
                if (existed=parent._cache.ContainsKey(id))
                {
                    oldValue = parent._cache[id];
                    parent._cache.Remove(id);
                }
            }

            public override void reverse()
            {
                if (existed)
                    parent._cache.Add(id, oldValue);
            }
        }
        class CacheEnumerator : IEnumerator<KeyValuePair<IDType, ObjectType>>
        {
            IEnumerator<KeyValuePair<IDType, byte[]>> e;
            CachedObjectBase<IDType,ObjectType> parent;
            public CacheEnumerator(CachedObjectBase<IDType, ObjectType> parent, IEnumerator<KeyValuePair<IDType, byte[]>> e)
            {
                this.e = e;
                this.parent = parent;
            }
            public KeyValuePair<IDType, ObjectType> Current
            {
                get {

                    return new KeyValuePair<IDType, ObjectType>(e.Current.Key,
                        this.parent.getObject(e.Current.Value));

                }
            }

            public void Dispose()
            {
                e.Dispose();
            }

            object System.Collections.IEnumerator.Current
            {
                get {
                    return this.Current;
                }
            }

            public bool MoveNext()
            {
                return e.MoveNext();
            }

            public void Reset()
            {
                e.Reset();
            }
        }
        List<Transaction> transactions = null;
        protected abstract ObjectType getObjectFromStore(IDType id);
        Dictionary<IDType, byte[]> _cache=new Dictionary<IDType,byte[]>();
        System.Runtime.Serialization.Formatters.Binary.BinaryFormatter serializer = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
        byte[] getBytes(ObjectType obj)
        {
            System.IO.MemoryStream ms = new MemoryStream();
            serializer.Serialize(ms, obj);
            ms.Seek(0, SeekOrigin.Begin);
            byte[] ret = new byte[ms.Length];
            ms.Read(ret, 0, ret.Length);
            ms.Dispose();
            return ret;
        }
        ObjectType getObject(byte[] bytes)
        {
            System.IO.MemoryStream ms = new MemoryStream(bytes);
            ObjectType obj = (ObjectType)serializer.Deserialize(ms);
            ms.Dispose();
            return obj;
        }
        public ObjectType this[IDType id]
        {
            get
            {
                byte[] ret;
                if (_cache.TryGetValue(id, out ret))
                {
                    return getObject(ret);
                }
                else
                {
                    ObjectType obj = getObjectFromStore(id);
                    ret = getBytes(obj);
                    _cache.Add(id, ret);
                    return obj;
                }
                
            }
            set
            {
                Transaction t = new AddTransaction(this, id, value);
                t.execute();
                if (transactions != null)
                    transactions.Add(t);
            }
        }
        public void delete(IDType id)
        {
            Transaction t = new RemoveTransaction(this, id);
            t.execute();
            if (transactions != null)
                transactions.Add(t);
            _cache.Remove(id);
            
        }

        public void BeginTransaction()
        {
            if (transactions != null)
                throw new InvalidOperationException("Transaction already started");
            transactions = new List<Transaction>();
        }

        public void CommitTransaction()
        {
            if (transactions == null)
                throw new InvalidOperationException("No transaction started");
            transactions = null;
        }

        public void RollBackTransaction()
        {
            if (transactions == null)
                throw new InvalidOperationException("No transaction started");
            for(int i=transactions.Count-1;i>=0;i--)
            {
                transactions[i].reverse();
            }
            transactions = null;
        }

        public IEnumerator<KeyValuePair<IDType, ObjectType>> GetEnumerator()
        {
            return new CacheEnumerator(this,_cache.GetEnumerator());
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return _cache.GetEnumerator();
        }
        public IEnumerable<IDType> keys
        {
            get
            {
                return _cache.Keys;
            }
        }
    }

    public class ObjectDiffCache<IDType,ObjectType>
    {
        Dictionary<IDType, ObjectType> _cache = new Dictionary<IDType, ObjectType>();
        HashSet<IDType> _deletedID= new HashSet<IDType>();
        HashSet<IDType> _changedID= new HashSet<IDType>();
        HashSet<IDType> _newID= new HashSet<IDType>();
        List<ObjectType> _newNoID= new List<ObjectType>();

        RetrieveObjectDelegate<IDType,ObjectType> rdel;
        IsValueNullDelegate<IDType> isIDNull;
        IsValueNullDelegate<ObjectType> isObjectNull;
        public ObjectDiffCache(RetrieveObjectDelegate<IDType, ObjectType> retrieveDelegate
            ,IsValueNullDelegate<IDType> isIDNull
            ,IsValueNullDelegate<ObjectType> isObjectNull
            )
        {
            rdel = retrieveDelegate;
            this.isIDNull = isIDNull;
            this.isObjectNull = isObjectNull;
        }
        public void addObject(IDType id,ObjectType obj)
        {
            if (!isObjectNull(this[id]))
                throw new System.InvalidOperationException("Object already exists");
            _cache.Add(id, obj);
            _newID.Add(id);
        }
        public void addObject(ObjectType obj)
        {
            _newNoID.Add(obj);
        }
        public void deleteObject(IDType id)
        {
            if (_deletedID.Contains(id))
                return;
            if(_cache.ContainsKey(id))
            {
                _cache.Remove(id);
                if (_newID.Contains(id))
                {
                    _newID.Remove(id);
                    return;
                }
                if (_changedID.Contains(id))
                    _changedID.Add(id);
            }
            _deletedID.Add(id);
        }
        public ObjectType this[IDType id]
        {
            get
            {
                ObjectType ret;
                if (_cache.TryGetValue(id,out ret))
                    return ret;
                ret = rdel(id);
                _cache.Add(id, ret);
                return ret;
            }
            set
            {
                ObjectType ret;
                if (_cache.TryGetValue(id, out ret))
                {
                    _cache[id] = value;
                    return;
                }
                ret = rdel(id);
                if (isObjectNull(ret))
                    addObject(id, value);
                else
                    _changedID.Add(id);
            }
        }
        public List<IDType> getDeletedList()
        {
            return _deletedID.ToList();
        }
        public List<Tuple<IDType,ObjectType>> getUpdateList()
        {
            List<Tuple<IDType, ObjectType>> ret = new List<Tuple<IDType, ObjectType>>();
            foreach (IDType id in _changedID)
                ret.Add(new Tuple<IDType, ObjectType>(id, _cache[id]));
            return ret;
        }
        public List<Tuple<IDType, ObjectType>> getNewWithIDList()
        {
            List<Tuple<IDType, ObjectType>> ret = new List<Tuple<IDType, ObjectType>>();
            foreach (IDType id in _newID)
                ret.Add(new Tuple<IDType, ObjectType>(id, _cache[id]));
            return ret;
        }
        public List<ObjectType> getNewWithNoIDList()
        {
            return _newNoID.ToList();
        }
    }
}
