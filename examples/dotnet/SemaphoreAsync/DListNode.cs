/**
 * Static class that supports intrusive doubly-linked lists wity types derived of DListNode 
 *
 **/

using System;
using System.Threading;

public class DListNode {
	// forward and back links
	public DListNode next, prev;
		
	// Initializes a doubly-linked list.
	public static void InitializeList(DListNode list) {
		list.next = list.prev = list;
	}

	// Returns the first entry of the list or null if the list is empty
	public static DListNode FirstEntry(DListNode list) {
		return list.next == list ? null : list.next;
	}
	
	// Returns true if the specified list is empty.
	public static bool IsListEmpty(DListNode list) {
		return list.next == list;
	}

	// Returns true of the specified ListNode is in a list
	public static bool IsInList(DListNode entry) {
		return Volatile.Read(ref entry.next) == null;
	}

	// Removes the specified entry from the list that contains it.
	public static bool Remove(DListNode entry) {
		DListNode next = entry.next, prev = entry.prev;
		next.prev = prev;
		prev.next = next;
		entry.next = entry.prev = null;		// mark as removed!
		return next == prev;
	}

	// Removes the specified entry if it is inserted in the list.
	public static bool RemoveIfInserted(DListNode entry) {
		DListNode next, prev;	
		if ((next = entry.next) == null)
			return false;
		prev = entry.prev;
		next.prev = prev;
		prev.next = next;
		entry.next = entry.prev = null;		// mark as removed!
		return true;
	}
	
	// Removes the entry that is at the front of the list.
	public static DListNode RemoveFirst(DListNode list) {
		DListNode entry = list.next, next = entry.next;
		list.next = next;
		next.prev = list;
		entry.next = entry.prev = null;		// mark as removed!
		return entry;
	}
	
	// Removes the entry that is at the tail of the list.
	public static DListNode RemoveLast(DListNode list) {
		DListNode entry = list.prev, prev = entry.prev;
		list.prev = prev;
		prev.next = list;
		entry.next = entry.prev = null;		// mark as removed and help GC!
		return entry;
	}
	
	// Inserts the specified entry at the tail of the list.
	public static void AddLast(DListNode list, DListNode entry) {
		DListNode prev = list.prev;			
		entry.next = list;
		entry.prev = prev;
		prev.next = entry;
		list.prev = entry;
	}
	
	// Inserts the specified entry at the head of the list.
	public static void AddFirst(DListNode list, DListNode entry) {
		DListNode next = list.next;	
		entry.next = next;
		entry.prev = list;
		next.prev = entry;
		list.next = entry;
	}
}
