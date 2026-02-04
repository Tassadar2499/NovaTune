import { defineConfig } from 'orval';

export default defineConfig({
  novatune: {
    input: {
      target: 'http://localhost:5000/openapi/v1.json',
    },
    output: {
      mode: 'tags-split',
      target: './src/generated',
      schemas: './src/generated/models',
      client: 'axios',
      override: {
        mutator: {
          path: './src/custom-instance.ts',
          name: 'customInstance',
        },
      },
    },
  },
});
