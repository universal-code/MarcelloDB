﻿using System;
using System.Collections.Generic;
using MarcelloDB.Collections;
using MarcelloDB.Index;
using MarcelloDB.Index.BTree;
using System.Collections;

namespace MarcelloDB
{
    internal class IndexEntryEnumerator<T, TKey>  : SessionBoundObject, IEnumerable<Entry<TKey>>
    {
        Collection Collection { get; set; }

        RecordIndex<TKey> Index { get; set; }

        BTreeWalkerRange<TKey> Range { get; set; }

        bool HasRange{ get { return this.Range != null; } }

        bool IsDescending { get; set; }

        public IndexEntryEnumerator(
            Collection collection,
            Session session,
            RecordIndex<TKey> index,
            bool isDescending = false
        ) : base(session)
        {
            this.Collection = collection;
            this.Index = index;
            this.IsDescending = isDescending;
        }

        public void SetRange(BTreeWalkerRange<TKey> range)
        {
            this.Range = range;
        }

        #region IEnumerable implementation
        IEnumerator<Entry<TKey>> IEnumerable<Entry<TKey>>.GetEnumerator()
        {
            //execute empty transaction, to apply any comitted but unapplied transactions
            this.Session.Transaction (() => { });
            lock(Session.SyncLock){
                try{
                    this.Collection.BlockModification = true;
                    var walker = this.Index.GetWalker();

                    if(this.IsDescending)
                    {
                        walker.Reverse();
                    }

                    if(this.HasRange)
                    {
                        walker.SetRange(this.Range);
                    }

                    var node = walker.Next();
                    while (node != null)
                    {
                        yield return node;
                        node = walker.Next();
                    }
                }
                finally
                {
                    this.Collection.BlockModification = false;
                }
            }
        }
        #endregion
        #region IEnumerable implementation
        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable<TKey>)this).GetEnumerator();
        }
        #endregion
    }
}

