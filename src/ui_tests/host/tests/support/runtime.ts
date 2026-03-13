export interface Credentials {
  displayName: string;
  email: string;
  password: string;
}

const defaultAdminEmail = process.env.UI_TESTS_ADMIN_EMAIL ?? 'markevich.roma@gmail.com';
const defaultAdminPassword = process.env.UI_TESTS_ADMIN_PASSWORD ?? '12345678';

export const adminCredentials = Object.freeze({
  email: defaultAdminEmail,
  password: defaultAdminPassword,
});

export function createUser(prefix: string): Credentials {
  const nonce = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
  const normalizedPrefix = prefix.toLowerCase().replace(/[^a-z0-9]+/g, '-');

  return {
    displayName: `${prefix} ${nonce}`,
    email: `${normalizedPrefix}-${nonce}@example.com`,
    password: 'SecurePassword123!',
  };
}
