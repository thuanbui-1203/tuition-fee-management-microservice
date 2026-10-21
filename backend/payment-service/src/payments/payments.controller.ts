import {
  Body,
  Controller,
  Get,
  Headers,
  HttpCode,
  HttpStatus,
  Param,
  Post,
  Query,
  Req,
  UseGuards,
} from '@nestjs/common';
import { ApiBearerAuth, ApiTags } from '@nestjs/swagger';
import { JwtGuard } from '../auth/jwt.guard';
import { CurrentUser, AuthUser, AuthedRequest } from '../auth/current-user';
import { PaymentsService } from './payments.service';
import { CreatePaymentDto } from './dto/create-payment.dto';
import { ConfirmPaymentDto } from './dto/confirm-payment.dto';

@ApiTags('payments')
@ApiBearerAuth()
@Controller('api/v1/payments')
@UseGuards(JwtGuard)
export class PaymentsController {
  constructor(private readonly payments: PaymentsService) {}

  // 'me' phải khai báo TRƯỚC ':id' để không bị route param nuốt mất.
  @Get('me')
  async history(
    @CurrentUser() user: AuthUser,
    @Query('page') page?: string,
    @Query('size') size?: string,
  ) {
    return this.payments.getHistory(
      user,
      page !== undefined ? Number(page) : 0,
      size !== undefined ? Number(size) : 20,
    );
  }

  @Get(':id')
  async getOne(@CurrentUser() user: AuthUser, @Param('id') paymentId: string) {
    return this.payments.getOne(user, paymentId);
  }

  @Post()
  @HttpCode(HttpStatus.CREATED)
  async create(
    @CurrentUser() user: AuthUser,
    @Body() dto: CreatePaymentDto,
    @Headers('idempotency-key') idempotencyKey: string,
    @Req() req: AuthedRequest,
  ) {
    const traceId = (req.headers['x-trace-id'] as string) ?? 'unknown';
    return this.payments.create(user, dto.mssv, idempotencyKey, traceId);
  }

  @Post(':id/confirm')
  @HttpCode(HttpStatus.OK)
  async confirm(
    @CurrentUser() user: AuthUser,
    @Param('id') paymentId: string,
    @Body() dto: ConfirmPaymentDto,
    @Req() req: AuthedRequest,
  ) {
    const traceId = (req.headers['x-trace-id'] as string) ?? 'unknown';
    return this.payments.confirm(user, paymentId, dto.otp, traceId);
  }

  @Post(':id/otp/resend')
  @HttpCode(HttpStatus.OK)
  async resendOtp(
    @CurrentUser() user: AuthUser,
    @Param('id') paymentId: string,
    @Req() req: AuthedRequest,
  ) {
    const traceId = (req.headers['x-trace-id'] as string) ?? 'unknown';
    return this.payments.resendOtp(user, paymentId, traceId);
  }
}
