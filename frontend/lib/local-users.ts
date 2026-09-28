export interface LocalUser {
  id: string;
  email: string;
  password: string;
  displayName: string;
  userName: string;
  roles: string[];
  avatarUrl?: string;
  createdAt: string;
}

export const SEED_USERS: LocalUser[] = [
  {
    id: "11111111-1111-1111-1111-111111111111",
    email: "admin@culinaryblog.local",
    password: "Admin@123",
    displayName: "Quản trị viên (Admin)",
    userName: "admin",
    roles: ["Admin", "Author"],
    createdAt: "2026-09-01T00:00:00.000Z",
  },
  {
    id: "22222222-2222-2222-2222-222222222222",
    email: "author@culinaryblog.local",
    password: "Author@123",
    displayName: "Đầu bếp mẫu (Author)",
    userName: "author",
    roles: ["Author"],
    createdAt: "2026-09-01T00:00:00.000Z",
  },
];

let inMemoryUsers: LocalUser[] = [...SEED_USERS];

function getNodeModules() {
  if (typeof process !== "undefined" && process.versions && process.versions.node) {
    try {
      // Dùng eval("require") để Next.js / Webpack không cố bundle 'fs' vào Edge runtime (middleware)
      const req = eval("require");
      const fs = req("fs");
      const path = req("path");
      return { fs, path };
    } catch {
      return null;
    }
  }
  return null;
}

function getUsersFilePath(): string | null {
  const node = getNodeModules();
  if (!node) return null;
  const cwd = process.cwd();
  const directPath = node.path.join(cwd, "data", "users.json");
  const frontendPath = node.path.join(cwd, "frontend", "data", "users.json");

  if (node.fs.existsSync(directPath)) return directPath;
  if (node.fs.existsSync(frontendPath)) return frontendPath;

  const targetDir = cwd.endsWith("frontend")
    ? node.path.join(cwd, "data")
    : node.path.join(cwd, "frontend", "data");

  if (!node.fs.existsSync(targetDir)) {
    node.fs.mkdirSync(targetDir, { recursive: true });
  }
  return node.path.join(targetDir, "users.json");
}

function ensureStorage(): LocalUser[] {
  const node = getNodeModules();
  if (!node) return inMemoryUsers;

  try {
    const usersFile = getUsersFilePath();
    if (!usersFile) return inMemoryUsers;

    if (!node.fs.existsSync(usersFile)) {
      node.fs.writeFileSync(usersFile, JSON.stringify(inMemoryUsers, null, 2), "utf8");
      return inMemoryUsers;
    }
    const raw = node.fs.readFileSync(usersFile, "utf8");
    const users = JSON.parse(raw) as LocalUser[];
    if (Array.isArray(users) && users.length > 0) {
      inMemoryUsers = users;
      return users;
    }
    return inMemoryUsers;
  } catch {
    return inMemoryUsers;
  }
}

export function getAllUsers(): LocalUser[] {
  return ensureStorage();
}

export function findLocalUser(identifier: string, password?: string): LocalUser | null {
  const users = ensureStorage();
  const raw = identifier.trim().toLowerCase();

  const user = users.find(
    (u) => u.email.toLowerCase() === raw || u.userName.toLowerCase() === raw
  );

  if (!user) return null;
  if (password !== undefined && user.password !== password) return null;

  return user;
}

export function registerLocalUser(userData: {
  account: string;
  password: string;
  fullName?: string;
}): LocalUser {
  const users = ensureStorage();
  const rawAccount = userData.account.trim().toLowerCase();

  const existing = users.find(
    (u) => u.email.toLowerCase() === rawAccount || u.userName.toLowerCase() === rawAccount
  );

  if (existing) {
    throw new Error("Tên tài khoản này đã được sử dụng. Vui lòng chọn tên khác!");
  }

  const isEmail = rawAccount.includes("@");
  const email = isEmail ? userData.account.trim() : `${userData.account.trim()}@culinaryblog.local`;
  const userName = userData.account.trim();

  const newUser: LocalUser = {
    id: typeof crypto !== "undefined" && crypto.randomUUID ? crypto.randomUUID() : String(Date.now()),
    email: email,
    password: userData.password,
    displayName: userData.fullName?.trim() || userName,
    userName: userName,
    roles: ["Author"],
    createdAt: new Date().toISOString(),
  };

  users.push(newUser);
  inMemoryUsers = users;

  const node = getNodeModules();
  if (node) {
    try {
      const usersFile = getUsersFilePath();
      if (usersFile) {
        node.fs.writeFileSync(usersFile, JSON.stringify(users, null, 2), "utf8");
      }
    } catch (err) {
      console.error("Lỗi lưu file users.json:", err);
    }
  }

  return newUser;
}
