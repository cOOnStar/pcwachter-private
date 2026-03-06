import { useMutation, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';

interface OptimisticUpdateOptions<TData, TVariables> {
  queryKey: string[];
  mutationFn: (variables: TVariables) => Promise<TData>;
  onSuccess?: (data: TData, variables: TVariables) => void;
  onError?: (error: Error, variables: TVariables) => void;
  successMessage?: string | ((data: TData, variables: TVariables) => string);
  errorMessage?: string | ((error: Error, variables: TVariables) => string);
  optimisticUpdate?: (variables: TVariables, oldData: any) => any;
}

/**
 * Hook für optimistische UI-Updates mit React Query
 * 
 * @example
 * const { mutate, isPending } = useOptimisticUpdate({
 *   queryKey: ['licenses'],
 *   mutationFn: async (licenseId) => {
 *     // API call
 *     return await renewLicense(licenseId);
 *   },
 *   optimisticUpdate: (licenseId, oldData) => {
 *     // Optimistic update - shows immediately
 *     return oldData.map(license => 
 *       license.id === licenseId 
 *         ? { ...license, status: 'Wird verlängert...' }
 *         : license
 *     );
 *   },
 *   successMessage: 'Lizenz erfolgreich verlängert',
 * });
 */
export function useOptimisticUpdate<TData = unknown, TVariables = void>({
  queryKey,
  mutationFn,
  onSuccess,
  onError,
  successMessage,
  errorMessage,
  optimisticUpdate,
}: OptimisticUpdateOptions<TData, TVariables>) {
  const queryClient = useQueryClient();

  return useMutation<TData, Error, TVariables, { previousData: unknown }>({
    mutationFn,
    
    // Optimistic update - runs immediately
    onMutate: async (variables) => {
      // Cancel any outgoing refetches
      await queryClient.cancelQueries({ queryKey });

      // Snapshot the previous value
      const previousData = queryClient.getQueryData(queryKey);

      // Optimistically update to the new value
      if (optimisticUpdate && previousData) {
        queryClient.setQueryData(queryKey, optimisticUpdate(variables, previousData));
      }

      // Return context object with the snapshotted value
      return { previousData };
    },

    // If the mutation fails, use the context returned from onMutate to roll back
    onError: (error, variables, context) => {
      if (context?.previousData !== undefined) {
        queryClient.setQueryData(queryKey, context.previousData);
      }
      
      const message = typeof errorMessage === 'function'
        ? errorMessage(error, variables)
        : errorMessage || 'Ein Fehler ist aufgetreten';
      
      toast.error(message);
      onError?.(error, variables);
    },

    // Always refetch after error or success
    onSettled: () => {
      queryClient.invalidateQueries({ queryKey });
    },

    onSuccess: (data, variables) => {
      const message = typeof successMessage === 'function'
        ? successMessage(data, variables)
        : successMessage;
      
      if (message) {
        toast.success(message);
      }
      
      onSuccess?.(data, variables);
    },
  });
}

/**
 * Hook für einfache Mutations mit Loading-States
 */
export function useMutationWithToast<TData = unknown, TVariables = void>(
  options: Omit<OptimisticUpdateOptions<TData, TVariables>, 'queryKey' | 'optimisticUpdate'>
) {
  return useMutation<TData, Error, TVariables>({
    mutationFn: options.mutationFn,
    
    onError: (error, variables) => {
      const message = typeof options.errorMessage === 'function'
        ? options.errorMessage(error, variables)
        : options.errorMessage || 'Ein Fehler ist aufgetreten';
      
      toast.error(message);
      options.onError?.(error, variables);
    },

    onSuccess: (data, variables) => {
      const message = typeof options.successMessage === 'function'
        ? options.successMessage(data, variables)
        : options.successMessage;
      
      if (message) {
        toast.success(message);
      }
      
      options.onSuccess?.(data, variables);
    },
  });
}
