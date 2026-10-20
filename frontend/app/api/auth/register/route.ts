import { NextResponse } from "next/server";
import { registerLocalUser } from "@/lib/local-users";
import { demoRegisterSchema, registerSchema } from "@/lib/validations/auth.schema";
import { isDemoAuthEnabled } from "@/lib/auth-mode";
import { registerErrorResponse, registerWithBackend } from "@/lib/backend-auth";
import type { RegisterInput } from "@/types/user";

export async function POST(req: Request) {
  try {
    const rawBody: unknown = await req.json();
    if (typeof rawBody !== "object" || rawBody === null || Array.isArray(rawBody))
      return NextResponse.json({ error: "Dữ liệu không hợp lệ" }, { status: 422 });
    const body = rawBody as Record<string, unknown>;
    
    // Hỗ trợ cả trường hợp gửi "account" hoặc ("userName" / "email")
    const rawAccount = body.account || body.userName || body.email;
    const rawPassword = body.password;
    const rawConfirm = body.confirmPassword || body.password;

    const schema = isDemoAuthEnabled() ? demoRegisterSchema : registerSchema;
    const validated = schema.safeParse({
      account: rawAccount,
      fullName: body.fullName ?? rawAccount,
      email: body.email,
      password: rawPassword,
      confirmPassword: rawConfirm,
    });

    if (!validated.success) {
      return NextResponse.json(
        { error: validated.error.issues[0]?.message || "Dữ liệu không hợp lệ" },
        { status: 400 }
      );
    }

    if (!isDemoAuthEnabled()) {
      // Parse riêng để giữ đúng type của schema production, không ép kiểu union demo.
      const production = registerSchema.parse({ ...validated.data, fullName: body.fullName ?? rawAccount });
      const request: RegisterInput = { fullName: production.fullName, email: production.email,
        userName: production.account, password: production.password };
      // Tuần 7 (FR-AUTH-001): tài khoản được tạo trong PostgreSQL bởi backend .NET. Lỗi backend
      // (email trùng 409, mật khẩu yếu 422, quá giới hạn 429, backend tắt) được đổi thành thông
      // báo cụ thể ở registerErrorResponse thay vì câu chung chung.
      try {
        const result = await registerWithBackend(request);
        return NextResponse.json({ success: true, user: result.user }, { status: 201 });
      } catch (error) {
        const { status, message } = registerErrorResponse(error);
        return NextResponse.json({ error: message }, { status });
      }
    }

    const user = registerLocalUser({
      account: validated.data.account,
      email: validated.data.email?.trim(),
      password: validated.data.password,
      fullName: typeof body.fullName === "string" ? body.fullName : validated.data.account,
    });

    return NextResponse.json(
      {
        success: true,
        message: "Đăng ký thành công!",
        user: {
          id: user.id,
          account: user.userName,
          email: user.email,
          displayName: user.displayName,
        },
      },
      { status: 201 }
    );
  } catch (err: unknown) {
    if (err instanceof SyntaxError)
      return NextResponse.json({ error: "JSON không hợp lệ" }, { status: 400 });
    return NextResponse.json(
      { error: isDemoAuthEnabled() && err instanceof Error ? err.message : "Không thể đăng ký. Vui lòng thử lại sau." },
      { status: isDemoAuthEnabled() ? 400 : 503 }
    );
  }
}
