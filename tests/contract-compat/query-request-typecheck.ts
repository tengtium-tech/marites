import type { MessageSearchRequest } from './generated/deferred-client';

const structuredSearch: MessageSearchRequest = {
  filter: {
    isDeadLetter: true,
  },
  sort: {
    field: 'capturedAt',
    direction: 'desc',
  },
  page: {
    limit: 50,
  },
};

void structuredSearch;
