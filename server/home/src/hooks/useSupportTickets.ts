import { useMemo, useState } from 'react';
import type { SupportTicket } from '../types';
import { usePortalBootstrap } from './usePortalBootstrap';

export function useSupportTickets() {
  const { data } = usePortalBootstrap();
  const tickets = (data?.supportTickets ?? []) as SupportTicket[];
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [sortBy, setSortBy] = useState<'createdAt' | 'lastUpdate'>('lastUpdate');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc');

  const filteredAndSortedTickets = useMemo(() => {
    let filtered = tickets.filter((ticket) => {
      const matchesSearch = 
        ticket.title.toLowerCase().includes(searchTerm.toLowerCase()) ||
        ticket.description.toLowerCase().includes(searchTerm.toLowerCase()) ||
        ticket.id.toString().includes(searchTerm);
      
      const matchesStatus = statusFilter === 'all' || ticket.status === statusFilter;

      return matchesSearch && matchesStatus;
    });

    // Sort
    filtered.sort((a, b) => {
      let compareValue = 0;

      switch (sortBy) {
        case 'createdAt':
          compareValue = new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime();
          break;
        case 'lastUpdate':
          compareValue = new Date(a.lastUpdate).getTime() - new Date(b.lastUpdate).getTime();
          break;
      }

      return sortOrder === 'asc' ? compareValue : -compareValue;
    });

    return filtered;
  }, [tickets, searchTerm, statusFilter, sortBy, sortOrder]);

  const toggleSort = (field: 'createdAt' | 'lastUpdate') => {
    if (sortBy === field) {
      setSortOrder(sortOrder === 'asc' ? 'desc' : 'asc');
    } else {
      setSortBy(field);
      setSortOrder('desc');
    }
  };

  return {
    tickets: filteredAndSortedTickets,
    searchTerm,
    setSearchTerm,
    statusFilter,
    setStatusFilter,
    sortBy,
    sortOrder,
    toggleSort,
  };
}
