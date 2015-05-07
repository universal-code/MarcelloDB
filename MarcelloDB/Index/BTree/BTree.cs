﻿using System;
using System.Diagnostics;
using System.Linq;

namespace MarcelloDB.Index.BTree
{
    public interface IBTree<TK, TP>
    {
        Entry<TK, TP> Search(TK key);

        void Insert(TK newKey, TP newPointer);

        void Delete(TK keyToDelete);

        Node<TK, TP> Root { get; } 
    }

    /// <summary>
    /// B tree implementation based on https://github.com/rdcastro/btree-dotnet
    /// </summary>
    public class BTree<TK, TP> : IBTree<TK, TP>
    {
        IBTreeDataProvider<TK, TP> DataProvider { get; set;}

        ObjectComparer Comparer { get; set; }

        public Node<TK, TP> Root { get; private set; } 

        public BTree(IBTreeDataProvider<TK, TP> dataProvider, int degree)
        {
            DataProvider = dataProvider;
            Comparer = new ObjectComparer();

            if (degree < 2)
            {
                throw new ArgumentException("BTree degree must be at least 2", "degree");
            }

            this.Root = DataProvider.GetRootNode(degree);
            this.Degree = degree;
            this.Height = 1;
        }

       

        public int Degree { get; private set; }

        public int Height { get; private set; }

        /// <summary>
        /// Searches a key in the BTree, returning the entry with it and with the pointer.
        /// </summary>
        /// <param name="key">Key being searched.</param>
        /// <returns>Entry for that key, null otherwise.</returns>
        public Entry<TK, TP> Search(TK key)
        {
            return this.SearchInternal(this.Root, key);
        }

        /// <summary>
        /// Inserts a new key associated with a pointer in the BTree. This
        /// operation splits nodes as required to keep the BTree properties.
        /// </summary>
        /// <param name="newKey">Key to be inserted.</param>
        /// <param name="newPointer">Pointer to be associated with inserted key.</param>
        public void Insert(TK newKey, TP newPointer)
        {
            // there is space in the root node
            if (!this.Root.HasReachedMaxEntries)
            {
                this.InsertNonFull(this.Root, newKey, newPointer);
                return;
            }

            // need to create new node and have it split
            Node<TK, TP> oldRoot = this.Root;

            this.Root = this.DataProvider.CreateNode(this.Degree);
            this.Root.ChildrenAddresses.Add(oldRoot.Address);
            this.SplitChild(this.Root, 0, oldRoot);
            this.InsertNonFull(this.Root, newKey, newPointer);

            this.Height++;
        }

        /// <summary>
        /// Deletes a key from the BTree. This operations moves keys and nodes
        /// as required to keep the BTree properties.
        /// </summary>
        /// <param name="keyToDelete">Key to be deleted.</param>
        public void Delete(TK keyToDelete)
        {
            this.DeleteInternal(this.Root, keyToDelete);

            // if root's last entry was moved to a child node, remove it
            if (this.Root.Entries.Count == 0 && !this.Root.IsLeaf)
            {
                this.Root = this.DataProvider.GetNode(this.Root.ChildrenAddresses.Single());
                this.Height--;
            }
        }

        /// <summary>
        /// Internal method to delete keys from the BTree
        /// </summary>
        /// <param name="node">Node to use to start search for the key.</param>
        /// <param name="keyToDelete">Key to be deleted.</param>
        private void DeleteInternal(Node<TK, TP> node, TK keyToDelete)
        {
            int i = node.Entries.TakeWhile(entry => Comparer.Compare(keyToDelete, entry.Key) > 0).Count();

            // found key in node, so delete if from it
            if (i < node.Entries.Count && Comparer.Compare(node.Entries[i].Key, keyToDelete) == 0)
            {
                this.DeleteKeyFromNode(node, keyToDelete, i);
                return;
            }

            // delete key from subtree
            if (!node.IsLeaf)
            {
                this.DeleteKeyFromSubtree(node, keyToDelete, i);
            }
        }

        /// <summary>
        /// Helper method that deletes a key from a subtree.
        /// </summary>
        /// <param name="parentNode">Parent node used to start search for the key.</param>
        /// <param name="keyToDelete">Key to be deleted.</param>
        /// <param name="subtreeIndexInNode">Index of subtree node in the parent node.</param>
        private void DeleteKeyFromSubtree(Node<TK, TP> parentNode, TK keyToDelete, int subtreeIndexInNode)
        {
            Node<TK, TP> childNode = this.DataProvider.GetNode(parentNode.ChildrenAddresses[subtreeIndexInNode]);

            // node has reached min # of entries, and removing any from it will break the btree property,
            // so this block makes sure that the "child" has at least "degree" # of nodes by moving an 
            // entry from a sibling node or merging nodes
            if (childNode.HasReachedMinEntries)
            {
                int leftIndex = subtreeIndexInNode - 1;
                Node<TK, TP> leftSibling = subtreeIndexInNode > 0 ? 
                      this.DataProvider.GetNode(parentNode.ChildrenAddresses[leftIndex]) : null;

                int rightIndex = subtreeIndexInNode + 1;
                Node<TK, TP> rightSibling = subtreeIndexInNode < parentNode.ChildrenAddresses.Count - 1
                    ? this.DataProvider.GetNode(parentNode.ChildrenAddresses[rightIndex])
                    : null;

                if (leftSibling != null && leftSibling.Entries.Count > this.Degree - 1)
                {
                    // left sibling has a node to spare, so this moves one node from left sibling 
                    // into parent's node and one node from parent into this current node ("child")
                    childNode.Entries.Insert(0, parentNode.Entries[subtreeIndexInNode]);
                    parentNode.Entries[subtreeIndexInNode] = leftSibling.Entries.Last();
                    leftSibling.Entries.RemoveAt(leftSibling.Entries.Count - 1);

                    if (!leftSibling.IsLeaf)
                    {
                        childNode.ChildrenAddresses.Insert(0, leftSibling.ChildrenAddresses.Last());
                        leftSibling.ChildrenAddresses.RemoveAt(leftSibling.ChildrenAddresses.Count - 1);    
                    }
                }
                else if (rightSibling != null && rightSibling.Entries.Count > this.Degree - 1)
                {
                    // right sibling has a node to spare, so this moves one node from right sibling 
                    // into parent's node and one node from parent into this current node ("child")
                    childNode.Entries.Add(parentNode.Entries[subtreeIndexInNode]);
                    parentNode.Entries[subtreeIndexInNode] = rightSibling.Entries.First();
                    rightSibling.Entries.RemoveAt(0);

                    if (!rightSibling.IsLeaf)
                    {
                        childNode.ChildrenAddresses.Add(rightSibling.ChildrenAddresses.First());
                        rightSibling.ChildrenAddresses.RemoveAt(0);
                    }
                }
                else
                {
                    // this block merges either left or right sibling into the current node "child"
                    if (leftSibling != null)
                    {
                        childNode.Entries.Insert(0, parentNode.Entries[subtreeIndexInNode - 1]);                     
                        var oldEntries = childNode.Entries;
                        childNode.Entries = leftSibling.Entries;
                        childNode.Entries.AddRange(oldEntries);
                        if (!leftSibling.IsLeaf)
                        {
                            var oldChildren = childNode.ChildrenAddresses;
                            childNode.ChildrenAddresses = leftSibling.ChildrenAddresses;
                            childNode.ChildrenAddresses.AddRange(oldChildren);
                        }

                        parentNode.ChildrenAddresses.RemoveAt(leftIndex);
                        parentNode.Entries.RemoveAt(subtreeIndexInNode - 1);
                    }
                    else
                    {
                        Debug.Assert(rightSibling != null, "Node should have at least one sibling");
                        childNode.Entries.Add(parentNode.Entries[subtreeIndexInNode]);
                        childNode.Entries.AddRange(rightSibling.Entries);
                        if (!rightSibling.IsLeaf)
                        {
                            childNode.ChildrenAddresses.AddRange(rightSibling.ChildrenAddresses);
                        }

                        parentNode.ChildrenAddresses.RemoveAt(rightIndex);
                        parentNode.Entries.RemoveAt(subtreeIndexInNode);
                    }
                }
            }

            // at this point, we know that "child" has at least "degree" nodes, so we can
            // move on - this guarantees that if any node needs to be removed from it to
            // guarantee BTree's property, we will be fine with that
            this.DeleteInternal(childNode, keyToDelete);
        }

        /// <summary>
        /// Helper method that deletes key from a node that contains it, be this
        /// node a leaf node or an internal node.
        /// </summary>
        /// <param name="node">Node that contains the key.</param>
        /// <param name="keyToDelete">Key to be deleted.</param>
        /// <param name="keyIndexInNode">Index of key within the node.</param>
        private void DeleteKeyFromNode(Node<TK, TP> node, TK keyToDelete, int keyIndexInNode)
        {
            // if leaf, just remove it from the list of entries (we're guaranteed to have
            // at least "degree" # of entries, to BTree property is maintained
            if (node.IsLeaf)
            {
                node.Entries.RemoveAt(keyIndexInNode);
                return;
            }

            Node<TK, TP> predecessorChild = this.DataProvider.GetNode(node.ChildrenAddresses[keyIndexInNode]);
            if (predecessorChild.Entries.Count >= this.Degree)
            {
                Entry<TK, TP> predecessor = this.DeletePredecessor(predecessorChild);
                node.Entries[keyIndexInNode] = predecessor;
            }
            else
            {
                Node<TK, TP> successorChild = this.DataProvider.GetNode(node.ChildrenAddresses[keyIndexInNode + 1]);
                if (successorChild.Entries.Count >= this.Degree)
                {
                    Entry<TK, TP> successor = this.DeleteSuccessor(predecessorChild);
                    node.Entries[keyIndexInNode] = successor;
                }
                else
                {
                    predecessorChild.Entries.Add(node.Entries[keyIndexInNode]);
                    predecessorChild.Entries.AddRange(successorChild.Entries);
                    predecessorChild.ChildrenAddresses.AddRange(successorChild.ChildrenAddresses);

                    node.Entries.RemoveAt(keyIndexInNode);
                    node.ChildrenAddresses.RemoveAt(keyIndexInNode + 1);

                    this.DeleteInternal(predecessorChild, keyToDelete);
                }
            }
        }

        /// <summary>
        /// Helper method that deletes a predecessor key (i.e. rightmost key) for a given node.
        /// </summary>
        /// <param name="node">Node for which the predecessor will be deleted.</param>
        /// <returns>Predecessor entry that got deleted.</returns>
        private Entry<TK, TP> DeletePredecessor(Node<TK, TP> node)
        {
            if (node.IsLeaf)
            {
                var result = node.Entries[node.Entries.Count - 1];
                node.Entries.RemoveAt(node.Entries.Count - 1);
                return result;
            }

            return this.DeletePredecessor(
                this.DataProvider.GetNode(node.ChildrenAddresses.Last())
            );
        }

        /// <summary>
        /// Helper method that deletes a successor key (i.e. leftmost key) for a given node.
        /// </summary>
        /// <param name="node">Node for which the successor will be deleted.</param>
        /// <returns>Successor entry that got deleted.</returns>
        private Entry<TK, TP> DeleteSuccessor(Node<TK, TP> node)
        {
            if (node.IsLeaf)
            {
                var result = node.Entries[0];
                node.Entries.RemoveAt(0);
                return result;
            }

            return this.DeletePredecessor(
                this.DataProvider.GetNode(node.ChildrenAddresses.First())
            );
        }

        /// <summary>
        /// Helper method that search for a key in a given BTree.
        /// </summary>
        /// <param name="node">Node used to start the search.</param>
        /// <param name="key">Key to be searched.</param>
        /// <returns>Entry object with key information if found, null otherwise.</returns>
        private Entry<TK, TP> SearchInternal(Node<TK, TP> node, TK key)
        {
            int i = node.Entries.TakeWhile(entry => Comparer.Compare(key, entry.Key) > 0).Count();

            if (i < node.Entries.Count && Comparer.Compare(node.Entries[i].Key, key) == 0)
            {
                return node.Entries[i];
            }
                
            return node.IsLeaf ? null : this.SearchInternal(
                this.DataProvider.GetNode(node.ChildrenAddresses[i]),
                key);

        }

        /// <summary>
        /// Helper method that splits a full node into two nodes.
        /// </summary>
        /// <param name="parentNode">Parent node that contains node to be split.</param>
        /// <param name="nodeToBeSplitIndex">Index of the node to be split within parent.</param>
        /// <param name="nodeToBeSplit">Node to be split.</param>
        private void SplitChild(Node<TK, TP> parentNode, int nodeToBeSplitIndex, Node<TK, TP> nodeToBeSplit)
        {
            var newNode = this.DataProvider.CreateNode(this.Degree);;

            parentNode.Entries.Insert(nodeToBeSplitIndex, nodeToBeSplit.Entries[this.Degree - 1]);
            parentNode.ChildrenAddresses.Insert(nodeToBeSplitIndex + 1, newNode.Address);

            newNode.Entries.AddRange(nodeToBeSplit.Entries.GetRange(this.Degree, this.Degree - 1));

            // remove also Entries[this.Degree - 1], which is the one to move up to the parent
            nodeToBeSplit.Entries.RemoveRange(this.Degree - 1, this.Degree);

            if (!nodeToBeSplit.IsLeaf)
            {
                newNode.ChildrenAddresses.AddRange(nodeToBeSplit.ChildrenAddresses.GetRange(this.Degree, this.Degree));
                nodeToBeSplit.ChildrenAddresses.RemoveRange(this.Degree, this.Degree);
            }
        }

        private void InsertNonFull(Node<TK, TP> node, TK newKey, TP newPointer)
        {
            int positionToInsert = node.Entries.TakeWhile(entry => Comparer.Compare(newKey, entry.Key) >= 0).Count();

            // leaf node
            if (node.IsLeaf)
            {
                node.Entries.Insert(positionToInsert, new Entry<TK, TP>() { Key = newKey, Pointer = newPointer });
                return;
            }

            // non-leaf
            Node<TK, TP> child = this.DataProvider.GetNode(node.ChildrenAddresses[positionToInsert]);
            if (child.HasReachedMaxEntries)
            {
                this.SplitChild(node, positionToInsert, child);
                if (Comparer.Compare(newKey, node.Entries[positionToInsert].Key) > 0)
                {
                    positionToInsert++;
                }
            }

            this.InsertNonFull(
                this.DataProvider.GetNode(node.ChildrenAddresses[positionToInsert]), 
                newKey, newPointer);
        }
    }
}