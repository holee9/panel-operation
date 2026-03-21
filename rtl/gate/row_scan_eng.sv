// row_scan_eng.sv — Row index counter and Gate ON/OFF timing generator
// Shared across all Gate IC types, parameterized by MAX_ROWS
module row_scan_eng
  import fpd_params_pkg::*;
#(
    parameter ROW_WIDTH = 12  // Supports up to 4096 rows
)(
    input  logic        clk,
    input  logic        rst_n,

    // Control
    input  logic        scan_start,
    input  logic        scan_abort,
    input  logic        scan_dir,         // 0=forward (0→N), 1=reverse (N→0)
    input  logic [ROW_WIDTH-1:0] cfg_nrows, // Number of rows to scan

    // Timing (from reg_bank)
    input  logic [11:0] cfg_tgate_on,     // Gate ON pulse width
    input  logic [7:0]  cfg_tgate_settle, // Gate settle time after OFF

    // Gate IC control outputs
    output logic [ROW_WIDTH-1:0] row_index,
    output logic        gate_on_pulse,    // Gate ON active
    output logic        gate_settle,      // Post-OFF settle period

    // Status
    output logic        scan_active,
    output logic        row_done,         // Current row complete
    output logic        scan_done         // All rows complete
);

  typedef enum logic [1:0] {
    SCAN_IDLE,
    SCAN_ON,
    SCAN_SETTLE
  } scan_state_t;

  scan_state_t scan_state;
  logic [11:0] gate_count;
  logic [7:0]  settle_count;

  always_ff @(posedge clk or negedge rst_n) begin
    if (!rst_n) begin
      row_index <= '0;
      gate_on_pulse <= 1'b0;
      gate_settle <= 1'b0;
      scan_active <= 1'b0;
      row_done <= 1'b0;
      scan_done <= 1'b0;
      gate_count <= '0;
      settle_count <= '0;
      scan_state <= SCAN_IDLE;
    end else begin
      row_done <= 1'b0;
      scan_done <= 1'b0;

      if (scan_abort) begin
        gate_on_pulse <= 1'b0;
        gate_settle <= 1'b0;
        scan_active <= 1'b0;
        scan_state <= SCAN_IDLE;
      end else begin
        case (scan_state)
          SCAN_IDLE: begin
            gate_on_pulse <= 1'b0;
            gate_settle <= 1'b0;
            scan_active <= 1'b0;
            if (scan_start) begin
              scan_active <= 1'b1;
              row_index <= scan_dir ? (cfg_nrows - 1'b1) : '0;
              gate_count <= '0;
              settle_count <= '0;
              scan_state <= SCAN_ON;
            end
          end

          SCAN_ON: begin
            gate_on_pulse <= 1'b1;
            if (gate_count + 12'd1 >= cfg_tgate_on) begin
              gate_on_pulse <= 1'b0;
              gate_count <= '0;
              scan_state <= SCAN_SETTLE;
            end else begin
              gate_count <= gate_count + 12'd1;
            end
          end

          default: begin
            gate_settle <= 1'b1;
            if (settle_count + 8'd1 >= cfg_tgate_settle) begin
              gate_settle <= 1'b0;
              settle_count <= '0;
              row_done <= 1'b1;
              if ((scan_dir && (row_index == '0)) ||
                  (!scan_dir && (row_index + 1'b1 >= cfg_nrows))) begin
                scan_done <= 1'b1;
                scan_active <= 1'b0;
                scan_state <= SCAN_IDLE;
              end else begin
                row_index <= scan_dir ? (row_index - 1'b1) : (row_index + 1'b1);
                scan_state <= SCAN_ON;
              end
            end else begin
              settle_count <= settle_count + 8'd1;
            end
          end
        endcase
      end
    end
  end

endmodule
