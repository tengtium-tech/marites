import { useQuery } from '@tanstack/react-query';
import {
  searchMessages,
  type MessageSearchRequest,
} from '../../../../tests/contract-compat/generated/deferred-client';

export const searchMessagesQueryKey = (request: MessageSearchRequest) =>
  ['messages', 'search', request] as const;

/**
 * Compatibility example for the future promoted operation. Keep it beside the
 * deferred contract until QUERY /messages becomes part of the served API.
 */
export function useSearchMessagesQuery(request: MessageSearchRequest) {
  return useQuery({
    queryKey: searchMessagesQueryKey(request),
    queryFn: () => searchMessages(request),
  });
}
