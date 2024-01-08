using System;
using System.Collections.Generic;

namespace Ddxy.GameServer.Logic.Aoi
{
    public enum AoiNodeLinkedListType
    {
        X = 0,
        Y = 1
    }

    public class AoiNodeLinkedList : LinkedList<AoiNode>
    {
        private readonly int _skipCount;

        private readonly AoiNodeLinkedListType _linkedListType;

        public AoiNodeLinkedList(int skip, AoiNodeLinkedListType linkedListType)
        {
            _skipCount = skip;
            _linkedListType = linkedListType;
        }

        public void Insert(AoiNode node)
        {
            if (_linkedListType == AoiNodeLinkedListType.X)
            {
                InsertX(node);
            }
            else
            {
                InsertY(node);
            }
        }

        private void InsertX(AoiNode node)
        {
            if (First == null)
            {
                node.XNode = AddFirst(node);
            }
            else
            {
                var slowCursor = First;
                var skip = Convert.ToInt32(Math.Ceiling(Convert.ToDouble(Count) / Convert.ToDouble(_skipCount)));
                if (Last?.Value.X > node.X)
                {
                    for (var i = 0; i < _skipCount; i++)
                    {
                        // 移动快指针
                        // ReSharper disable once PossibleNullReferenceException
                        var fastCursor = FastCursor(skip, slowCursor.Value);
                        // 如果快指针的值小于插入的值，把快指针赋给慢指针，当做当前指针。
                        if (fastCursor.Value.X < node.X)
                        {
                            slowCursor = fastCursor;
                            continue;
                        }

                        // 慢指针移动到快指针位置
                        while (slowCursor != null)
                        {
                            if (slowCursor.Value.X >= node.X)
                            {
                                node.XNode = new LinkedListNode<AoiNode>(node);
                                AddBefore(slowCursor, node.XNode);
                                return;
                            }

                            slowCursor = slowCursor.Next;
                        }
                    }
                }

                node.XNode ??= AddLast(node);
            }
        }

        private void InsertY(AoiNode node)
        {
            if (First == null)
            {
                node.YNode = AddFirst(node);
            }
            else
            {
                var slowCursor = First;
                var skip = Convert.ToInt32(Math.Ceiling(Convert.ToDouble(Count) / Convert.ToDouble(_skipCount)));
                if (Last?.Value.Y > node.Y)
                {
                    for (var i = 0; i < _skipCount; i++)
                    {
                        // 移动快指针
                        // ReSharper disable once PossibleNullReferenceException
                        var fastCursor = FastCursor(skip, slowCursor.Value);
                        // 如果快指针的值小于插入的值，把快指针赋给慢指针，当做当前指针。
                        if (fastCursor.Value.Y <= node.Y)
                        {
                            slowCursor = fastCursor;
                            continue;
                        }

                        // 慢指针移动到快指针位置
                        while (slowCursor != null)
                        {
                            if (slowCursor.Value.Y >= node.Y)
                            {
                                node.YNode = new LinkedListNode<AoiNode>(node);
                                AddBefore(slowCursor, node.YNode);
                                return;
                            }

                            slowCursor = slowCursor.Next;
                        }
                    }
                }

                node.YNode ??= AddLast(node);
            }
        }

        private LinkedListNode<AoiNode> FastCursor(int skip, AoiNode currentNode)
        {
            var skipLink = currentNode;
            switch (_linkedListType)
            {
                case AoiNodeLinkedListType.X:
                {
                    for (var i = 1; i <= skip; i++)
                    {
                        if (skipLink.XNode.Next == null) break;
                        skipLink = skipLink.XNode.Next.Value;
                    }

                    return skipLink.XNode;
                }
                case AoiNodeLinkedListType.Y:
                {
                    for (var i = 1; i <= skip; i++)
                    {
                        if (skipLink.YNode.Next == null) break;
                        skipLink = skipLink.YNode.Next.Value;
                    }

                    return skipLink.YNode;
                }
                default:
                    return null;
            }
        }
    }
}