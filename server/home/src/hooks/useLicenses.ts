import { useMemo, useState } from 'react';
import type { License } from '../types';
import { usePortalBootstrap } from './usePortalBootstrap';

export function useLicenses() {
  const { data } = usePortalBootstrap();
  const licenses = (data?.licenses ?? []) as License[];
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [typeFilter, setTypeFilter] = useState<string>('all');
  const [sortBy, setSortBy] = useState<'name' | 'validUntil' | 'status'>('name');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('asc');

  const filteredAndSortedLicenses = useMemo(() => {
    let filtered = licenses.filter((license) => {
      const matchesSearch = 
        license.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
        license.key.toLowerCase().includes(searchTerm.toLowerCase());
      
      const matchesStatus = statusFilter === 'all' || license.status === statusFilter;
      const matchesType = typeFilter === 'all' || license.type === typeFilter;

      return matchesSearch && matchesStatus && matchesType;
    });

    // Sort
    filtered.sort((a, b) => {
      let compareValue = 0;

      switch (sortBy) {
        case 'name':
          compareValue = a.name.localeCompare(b.name);
          break;
        case 'validUntil':
          // Parse German date format DD.MM.YYYY
          const parseDate = (dateStr: string) => {
            const [day, month, year] = dateStr.split('.');
            return new Date(parseInt(year), parseInt(month) - 1, parseInt(day)).getTime();
          };
          compareValue = parseDate(a.validUntil) - parseDate(b.validUntil);
          break;
        case 'status':
          compareValue = a.status.localeCompare(b.status);
          break;
      }

      return sortOrder === 'asc' ? compareValue : -compareValue;
    });

    return filtered;
  }, [licenses, searchTerm, statusFilter, typeFilter, sortBy, sortOrder]);

  const toggleSort = (field: 'name' | 'validUntil' | 'status') => {
    if (sortBy === field) {
      setSortOrder(sortOrder === 'asc' ? 'desc' : 'asc');
    } else {
      setSortBy(field);
      setSortOrder('asc');
    }
  };

  return {
    licenses: filteredAndSortedLicenses,
    searchTerm,
    setSearchTerm,
    statusFilter,
    setStatusFilter,
    typeFilter,
    setTypeFilter,
    sortBy,
    sortOrder,
    toggleSort,
  };
}
