import { NextResponse } from "next/server";
import { registerLocalUser } from "@/lib/local-users";
import { registerSchema } from "@/lib/validations/auth.schema";

export async function POST(req: Request) {
  try {
    const body = await req.json();
    
    // Hỗ trợ cả trường hợp gửi "account" hoặc ("userName" / "email")
    const rawAccount = body.account || body.userName || body.email;
    const rawPassword = body.password;
    const rawConfirm = body.confirmPassword || body.password;

    const validated = registerSchema.safeParse({
      account: rawAccount,
      password: rawPassword,
      confirmPassword: rawConfirm,
    });

    if (!validated.success) {
      return NextResponse.json(
        { error: validated.error.issues[0]?.message || "Dữ liệu không hợp lệ" },
        { status: 400 }
      );
    }

    const user = registerLocalUser({
      account: validated.data.account,
      password: validated.data.password,
      fullName: body.fullName || validated.data.account,
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
  } catch (err: any) {
    return NextResponse.json(
      { error: err.message || "Đăng ký thất bại. Vui lòng thử lại!" },
      { status: 400 }
    );
  }
}
