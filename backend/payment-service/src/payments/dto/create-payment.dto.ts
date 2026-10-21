import { IsString, Matches } from 'class-validator';
import { ApiProperty } from '@nestjs/swagger';

export class CreatePaymentDto {
  @ApiProperty({ example: '521H0001', description: 'MSSV cần đóng học phí' })
  @IsString()
  @Matches(/^[0-9A-Za-z]{4,20}$/, {
    message: 'MSSV không hợp lệ (4-20 ký tự chữ/số)',
  })
  mssv!: string;
}
