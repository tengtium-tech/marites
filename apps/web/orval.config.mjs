export default {
  maritesServed: {
    input: '../../docs/07-data-and-contracts/openapi/openapi.yaml',
    output: {
      target: './src/api/generated/client.ts',
      client: 'fetch',
      mode: 'single',
    },
  },
  maritesDeferredCompatibility: {
    input: '../../docs/07-data-and-contracts/openapi/openapi.deferred.yaml',
    output: {
      target: '../../tests/contract-compat/generated/deferred-client.ts',
      client: 'fetch',
      mode: 'single',
    },
  },
};
