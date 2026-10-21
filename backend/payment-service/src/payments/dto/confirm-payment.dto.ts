import { IsString, Length, Matches } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';

export class ConfirmPaymentDto {
  @ApiProperty({ example: '482913', description: 'Mã OTP 6 chữ số' })
  @IsString()
  @Length(6, 6, { message: 'OTP phải gồm 6 chữ số' })
  @Matches(/^[0-9]{6}$/, { message: 'OTP chỉ gồm chữ số' })
  otp!: string;
}
